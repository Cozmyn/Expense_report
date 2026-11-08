using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class FinancialWorkflowTests
{
    static async Task<HttpClient> Login(Factory f, string username)
    {
        var c = f.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync("/api/auth/login", new { username, password = "Valid-test-Password1!" })
            ).StatusCode
        );
        token = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return c;
    }

    static void Key(HttpClient c)
    {
        c.DefaultRequestHeaders.Remove("Idempotency-Key");
        c.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }

    static ExpenseData Data(decimal amount = 100) =>
        new(
            "Shop",
            null,
            "FX-1",
            "2026-01-01",
            "DE",
            "Receipt",
            "GENERAL",
            "Business expense",
            "Personal",
            null,
            "CC",
            null,
            amount,
            "EUR",
            [],
            false,
            false,
            "EUR"
        );

    static ClaimsPrincipal FinanceUser() =>
        new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "finance"), new(ClaimTypes.Role, "Finance")], "test"));

    [Theory]
    [InlineData(InvoiceRounding.HalfAwayFromZero, 512.35)]
    [InlineData(InvoiceRounding.HalfToEven, 512.34)]
    public async Task FinanceFxAdjustmentKeepsManagerRevisionAndApprovedEstimate(
        InvoiceRounding rounding,
        decimal expected
    )
    {
        await using var f = new Factory();
        await f.Seed();
        Guid id,
            line;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
            var r = new Report
            {
                EmployeeId = employee.Id,
                State = States.Finance,
                Revision = 1,
                ApprovedPersonal = 500,
                Purpose = "FX approval",
                Lines =
                [
                    new()
                    {
                        Position = 1,
                        EstimatedRon = 500,
                        FxVersion = "estimate-1",
                        Json = Serialization.Write(Data()),
                    },
                ],
            };
            db.FinancePolicies.Add(
                new()
                {
                    Currency = "EUR",
                    From = new(2020, 1, 1),
                    Version = 1,
                    Json = Serialization.Write(new CurrencyFinancePolicy(Rounding: rounding)),
                    Actor = "finance",
                    Reason = "Rounding selection",
                }
            );
            db.Reports.Add(r);
            db.Decisions.Add(
                new()
                {
                    ReportId = r.Id,
                    Revision = 1,
                    Action = "approve",
                    Actor = "manager",
                }
            );
            await db.SaveChangesAsync();
            id = r.Id;
            line = r.Lines[0].Id;
        }
        var c = await Login(f, "finance");
        Key(c);
        var proposed = await c.PostAsJsonAsync(
            $"/api/finance/reports/{id}/lines/{line}/fx-proposals",
            new FxProposalInput(1, "MANUAL", "5.12345", null, null, "Final rate from Finance")
        );
        Assert.True(proposed.IsSuccessStatusCode, await proposed.Content.ReadAsStringAsync());
        var proposal = (await proposed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("proposal")
            .GetProperty("id")
            .GetGuid();
        Key(c);
        var approved = await c.PostAsJsonAsync(
            $"/api/finance/reports/{id}/lines/{line}/fx-decisions",
            new FxDecisionInput(2, proposal, "Confirm final exchange rate")
        );
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        var view = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(States.Finance, view.GetProperty("state").GetString());
        Assert.Equal(1, view.GetProperty("revision").GetInt32());
        Assert.Equal(expected, view.GetProperty("total").GetDecimal());
        Assert.Equal(500, view.GetProperty("lines")[0].GetProperty("estimatedRon").GetDecimal());
        using var verify = f.Services.CreateScope();
        var data = verify.ServiceProvider.GetRequiredService<Db>();
        Assert.Single(await data.Decisions.ToListAsync());
        Assert.Empty(await data.Snapshots.ToListAsync());
        Assert.Empty(await data.Notices.ToListAsync());
        Assert.Single(await data.FxDecisions.ToListAsync());
    }

    [Fact]
    public async Task PoliciesAreFinanceOnlyAndLatestEstimateDoesNotRewriteOldLines()
    {
        await using var f = new Factory();
        await f.Seed();
        var input = new FinancePolicyInput(
            "EUR",
            new(2020, 1, 1),
            new(2, 6, InvoiceRounding.HalfToEven, 2, "5.111111"),
            "Finance currency policy"
        );
        var employee = await Login(f, "alice");
        Key(employee);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await employee.PostAsJsonAsync("/api/finance/currency-policies", input)).StatusCode
        );
        var admin = await Login(f, "admin");
        Key(admin);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync("/api/finance/currency-policies", input)).StatusCode
        );
        var finance = await Login(f, "finance");
        Key(finance);
        Assert.Equal(
            HttpStatusCode.OK,
            (await finance.PostAsJsonAsync("/api/finance/currency-policies", input)).StatusCode
        );
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        Assert.Equal(5.111111m, (await FinancialCalculations.Estimate(db, "EUR")).Rate);
        var r = new Report { Purpose = "New entries" };
        db.Reports.Add(r);
        await db.SaveChangesAsync();
        var l = new Line { ReportId = r.Id };
        await new Reports(db).Apply(l, Data());
        Assert.Equal(511.11m, l.EstimatedRon);
        var version = await db.FinancePolicies.SingleAsync();
        version.Reason = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2.01, true)]
    [InlineData(-2, false)]
    [InlineData(-2.01, true)]
    public void RoundingBoundaryAndSignedDocuments(decimal difference, bool blocking)
    {
        var rate = new Master
        {
            Kind = "vat",
            From = new(2020, 1, 1),
            Json = "{\"rate\":21}",
        };
        var sign = Math.Sign(difference);
        var data = Data(sign * 121 + difference) with
        {
            Country = "RO",
            HasCompanyCui = true,
            Currency = "RON",
            TaxCurrency = "RON",
            Taxes = [new(rate.Id, sign * 100, sign * 21)],
        };
        var line = new Line { Json = Serialization.Write(data), EstimatedRon = data.Amount };
        var result = FinancialCalculations.Preview(line, [rate], new(), "test");
        Assert.Equal(blocking, result.Issues.Any(i => i.Blocking));
        Assert.Equal(difference, result.RoundingRon);
        Assert.Equal(100 * sign, result.PostingData.Taxes[0].Base);
        if (sign < 0)
            Assert.Contains(result.Issues, i => i.Code == "negative_amount" && !i.Blocking);
    }

    [Fact]
    public async Task AcceptedCalculationsAreImmutableAndStaleAfterFxChange()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var r = new Report
        {
            State = States.Finance,
            Purpose = "Calculation ledger",
            Lines =
            [
                new()
                {
                    EstimatedRon = 500,
                    FxVersion = "original",
                    Json = Serialization.Write(Data()),
                    Evidence = [new() { Scan = "Clean", Hash = "original" }],
                },
            ],
        };
        db.Reports.Add(r);
        await db.SaveChangesAsync();
        var line = r.Lines[0];
        var decision = new ExpenseFxDecision
        {
            LineId = line.Id,
            InputHash = FinancialCalculations.SourceHash(line),
            FinalRon = 500,
            Source = "MANUAL",
            Actor = "finance",
            Reason = "Verified rate",
        };
        db.FxDecisions.Add(decision);
        line.FinanceRon = 500;
        line.FxDecisionId = decision.Id;
        await FinancialCalculations.Accept(db, new(db), r, line, FinanceUser(), "Verified against original document");
        await db.SaveChangesAsync();
        Assert.Empty(await FinancialCalculations.ExportProblems(db, r));
        line.FinanceRon = 501;
        Assert.Contains(await FinancialCalculations.ExportProblems(db, r), p => p.Code == "calculation_stale");
        var saved = await db.Calculations.SingleAsync();
        saved.Result = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void NegativeBankRowsKeepSameImportAndAllocationFlow()
    {
        var rows = Finance.Parse(
            new(
                Guid.NewGuid(),
                "reference,date,merchant,amount,currency,settledRon\nNEG,2026-01-01,Shop,-100,EUR,-501.23",
                new()
            )
        );
        Assert.Equal(-501.23m, Assert.Single(rows).SettledRon);
    }

    [Fact]
    public void NegativeExpenseUsesTheSame24ColumnExportWithReversedSide()
    {
        var data = Data(-100) with { Currency = "RON" };
        var line = new Line { EstimatedRon = -100, Json = Serialization.Write(data) };
        var masters = new List<Master>
        {
            new()
            {
                Kind = "accounting",
                Code = "GENERAL",
                From = new(2020, 1, 1),
                Json = "{\"mainAccount\":\"TEST-GL\",\"localAccount\":\"TEST-LOCAL\"}",
            },
            new()
            {
                Kind = "costCenters",
                Code = "CC",
                From = new(2020, 1, 1),
                Json = "{\"erpCode\":\"CC\"}",
            },
            new()
            {
                Kind = "erpSettings",
                Code = "default",
                From = new(2020, 1, 1),
                Json = "{\"approvalReference\":\"TEST\",\"clearingAccount\":\"CLEAR\",\"clearingLocal\":\"LOCAL\"}",
            },
        };
        var rows = new Posting().Build(new() { Lines = [line] }, new() { Worker = "employee" }, masters).Rows;
        var row = Assert.Single(rows);
        Assert.Equal(24, row.Length);
        Assert.Equal("", row[13]);
        Assert.Equal("100.00", row[14]);
        Assert.Equal("employee", row[11]);
    }

    [Fact]
    public async Task EntryNeverSilentlyRoundsOriginalDocumentGross()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var r = new Report { Purpose = "Exact document gross" };
        db.Reports.Add(r);
        await db.SaveChangesAsync();
        var line = new Line { ReportId = r.Id };
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            new Reports(db).Apply(line, Data(1.005m) with { Currency = "RON" })
        );
        Assert.Equal("currency_precision", error.Code);
        Assert.Equal("{}", line.Json);
    }
}
