using System.Security.Claims;
using System.Text;
using Decont;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class PlatformTests
{
    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("100,00", 100)]
    [InlineData("100.00", 100)]
    public void OcrReadsLocalizedAmounts(string value, decimal expected) =>
        Assert.Equal(expected, ReceiptParser.Number(value));

    [Fact]
    public void OcrKeepsSupplierPrefixAndExtractsMultiplePagesAndVatWithoutPretendingConfidence()
    {
        var result = ReceiptParser.Parse(
            [
                new(
                    1,
                    "Supplier SRL\nCUI RO18546294\nBON FISCAL NR B001\n01.01.2026\nTVA 0% 100.00 0.00\nTVA 11% 100.00 11.00\nTVA 21% 100.00 21.00\nTOTAL 332.00 RON"
                ),
                new(2, "INVOICE NO INV002\n02.01.2026\nTOTAL 1.234,56 EUR"),
            ],
            "35898037"
        );
        Assert.Equal(2, result.Candidates.Count);
        var first = result.Candidates[0];
        Assert.Equal("18546294", first.Fields["cui"].Value);
        Assert.Equal("Supplier SRL", first.Fields["supplier"].Value);
        Assert.Equal("B001", first.Fields["invoice"].Value);
        Assert.Equal("332.00", first.Fields["amount"].Value);
        Assert.Equal(3, first.Taxes.Count);
        Assert.Equal(332, first.Taxes.Sum(t => t.Base + t.Vat));
        Assert.Equal("1234.56", result.Candidates[1].Fields["amount"].Value);
        Assert.All(
            first.Fields.Values,
            f =>
            {
                Assert.Equal("ReviewRequired", f.State);
                Assert.Null(f.Confidence);
            }
        );
        Assert.Equal("RO", first.Fields["country"].Value);
        Assert.Equal("false", first.Fields["hasCompanyCui"].Value);
        Assert.Equal(2, result.Pages.Count);
    }

    [Fact]
    public void OcrOnlyConfirmsCompanyCuiWhenPrintedAndPreservesExactPaidVat()
    {
        var result = ReceiptParser.Parse(
            [new(1, "FACTURA NR A1\nCUI 18546294\nClient CUI 12345674\nTVA 11% 100.00 10.99\nTOTAL 110.99 RON")],
            "12345674"
        );
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("true", candidate.Fields["hasCompanyCui"].Value);
        Assert.Equal(10.99m, Assert.Single(candidate.Taxes).Vat);
        Assert.Equal("18546294", candidate.Fields["cui"].Value);
    }

    [Fact]
    public void SemicolonCsvAndQuotedFieldsArePreserved()
    {
        var rows = SpreadsheetImports.Read(
            "master.csv",
            Encoding.UTF8.GetBytes("code;nameRo;json\r\nA;\"Name; with separator\";\"{}\"")
        );
        Assert.Equal("Name; with separator", rows[1][1]);
        Assert.Equal(rows.SelectMany(r => r), Csv.Read(SpreadsheetImports.CsvText(rows)).SelectMany(r => r));
    }

    [Fact]
    public async Task MasterBatchRejectsOverlapsAndAllowsFutureRates()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var a = new MasterInput(
            "vat",
            "FUTURE",
            "Cotă nouă",
            "New rate",
            "{\"rate\":22}",
            new(2027, 1, 1),
            null,
            true,
            "Finance approved"
        );
        await SpreadsheetImports.Validate(a, db);
        await Assert.ThrowsAsync<DomainException>(() =>
            SpreadsheetImports.Validate(a with { From = new(2027, 2, 1) }, db, [a])
        );
        await Assert.ThrowsAsync<DomainException>(() =>
            SpreadsheetImports.Validate(a with { Json = "{\"rate\":5}" }, db)
        );
        await SpreadsheetImports.Validate(a with { Json = "{\"rate\":5}", Active = false }, db);
    }

    [Fact]
    public void EmployeeCannotOverrideAccountingAndFinanceNeedsReason()
    {
        var d = new ExpenseData(
            "",
            null,
            "",
            "2026-01-01",
            "ZZ",
            "Receipt",
            "GENERAL",
            "Expense",
            "Personal",
            null,
            "CC",
            null,
            100,
            "RON",
            [],
            false,
            false,
            "RON",
            new("GL", "LOCAL", null)
        );
        ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity([new(ClaimTypes.Role, role)]));
        Assert.Throws<DomainException>(() =>
            PlatformFeatures.AccountingPermission(Principal("Employee"), d, "Approved")
        );
        Assert.Throws<DomainException>(() => PlatformFeatures.AccountingPermission(Principal("Finance"), d, null));
        PlatformFeatures.AccountingPermission(Principal("Finance"), d, "Verified mapping");
    }

    [Fact]
    public async Task DatedAssignmentDisablesOnlyItsEffectivePeriod()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var user = await db.Users.SingleAsync(u => u.UserName == "alice");
        db.Assignments.Add(
            new()
            {
                UserId = user.Id,
                ManagerId = "manager",
                Active = false,
                From = new(2027, 1, 1),
                To = new(2027, 2, 1),
            }
        );
        await db.SaveChangesAsync();
        Assert.True(await PlatformFeatures.Active(db, user, new(2026, 1, 1)));
        Assert.False(await PlatformFeatures.Active(db, user, new(2027, 1, 10)));
        Assert.True(await PlatformFeatures.Active(db, user, new(2027, 2, 2)));
    }

    [Fact]
    public async Task CommentsAndExportDataAreImmutable()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var r = new Report();
        db.Reports.Add(r);
        var comment = new LineComment { ReportId = r.Id, Text = "Review evidence" };
        db.Comments.Add(comment);
        var batch = new Batch { ReportId = r.Id, Rows = "[]" };
        db.Batches.Add(batch);
        await db.SaveChangesAsync();
        comment.Text = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(comment).State = EntityState.Unchanged;
        batch.Rows = "[1]";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ReminderWindowsDoNotCreateRepeatedNotices()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
        db.Masters.Add(
            new()
            {
                Kind = "notificationRules",
                Code = "reminders",
                From = new(2020, 1, 1),
                Json = "{\"afterHours\":1,\"repeatHours\":24,\"escalateHours\":48}",
            }
        );
        db.Masters.Add(
            new()
            {
                Kind = "notificationRules",
                Code = "default",
                From = new(2020, 1, 1),
                Json = "{\"email\":false}",
            }
        );
        db.Reports.Add(
            new()
            {
                ManagerId = manager.Id,
                State = States.Manager,
                Updated = DateTime.UtcNow.AddHours(-2),
            }
        );
        await db.SaveChangesAsync();
        var service = new Reports(db);
        await NotificationPolicies.Remind(db, service, DateTime.UtcNow);
        await db.SaveChangesAsync();
        await NotificationPolicies.Remind(db, service, DateTime.UtcNow);
        await db.SaveChangesAsync();
        Assert.Single(await db.Notices.ToListAsync());
        Assert.Empty(await db.Jobs.ToListAsync());
    }

    [Fact]
    public async Task FinanceReapprovalPolicyCanBeDisabledExplicitly()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        db.Masters.Add(
            new()
            {
                Kind = "policies",
                Code = "workflow",
                From = new(2020, 1, 1),
                Json = "{\"requireReapprovalOnPersonalIncrease\":false}",
            }
        );
        var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
        var owner = await db.Users.SingleAsync(u => u.UserName == "manager");
        var r = new Report
        {
            EmployeeId = employee.Id,
            ManagerId = owner.Id,
            State = States.Finance,
            ApprovedPersonal = 50,
            Lines =
            [
                new()
                {
                    EstimatedRon = 100,
                    Json = Serialization.Write(
                        new ExpenseData(
                            "",
                            null,
                            "",
                            "2026-01-01",
                            "ZZ",
                            "Receipt",
                            "GENERAL",
                            "Expense",
                            "Personal",
                            null,
                            "CC",
                            null,
                            100,
                            "RON",
                            [],
                            false
                        )
                    ),
                },
            ],
        };
        db.Reports.Add(r);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "finance"), new(ClaimTypes.Role, "Finance")])
        );
        await new Reports(db).Reapproval(r, principal);
        Assert.Equal(States.Finance, r.State);
        Assert.Equal(0, r.Revision);
        Assert.Empty(await db.Snapshots.ToListAsync());
    }
}
