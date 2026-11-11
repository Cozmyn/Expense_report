using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class OcrReviewTests
{
    static async Task<HttpClient> Login(Factory f, string name)
    {
        var c = f.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync("/api/auth/login", new { username = name, password = "Valid-test-Password1!" })
            ).StatusCode
        );
        csrf = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return c;
    }

    static void Key(HttpClient c)
    {
        c.DefaultRequestHeaders.Remove("Idempotency-Key");
        c.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }

    static ExpenseData Data() =>
        new(
            "Vendor",
            null,
            "TEST",
            "2026-01-01",
            "DE",
            "InvoiceWithoutCui",
            "GENERAL",
            "Verified receipt",
            "Personal",
            null,
            "CC",
            null,
            10,
            "RON",
            []
        );

    static async Task<(Guid Report, Guid Document)> Fixture(Factory f, int pages = 2, string? sourceText = null, string receiptMode = "Auto")
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
        var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
        var report = new Report
        {
            EmployeeId = employee.Id,
            ManagerId = manager.Id,
            Purpose = "OCR review",
        };
        var source = new ReportAttachment
        {
            ReportId = report.Id,
            Name = "synthetic.pdf",
            Mime = "application/pdf",
            Key = "test",
            Scan = "Clean",
            Hash = "SYNTHETIC",
            ReceiptMode = receiptMode,
        };
        report.Attachments.Add(source);
        db.Reports.Add(report);
        db.Masters.AddRange(
            new Master
            {
                Kind = "expenseTypes",
                Code = "GENERAL",
                From = new(2020, 1, 1),
            },
            new Master
            {
                Kind = "costCenters",
                Code = "CC",
                From = new(2020, 1, 1),
            }
        );
        await db.SaveChangesAsync();
        var result = ReceiptParser.Parse(
            Enumerable
                .Range(1, pages)
                .Select(p => new OcrPage(p, sourceText ?? ("INVOICE NO A" + p + "\n01.01.2026\nTOTAL 10.00 RON")))
                .ToList(),
            null
        );
        source.Ocr = Serialization.Write(result);
        await OcrReviews.Import(db, report.Id, source.Id, result);
        await db.SaveChangesAsync();
        return (report.Id, source.Id);
    }

    static async Task<JsonElement> View(HttpClient c, Guid report, Guid source) =>
        await c.GetFromJsonAsync<JsonElement>($"/api/reports/{report}/documents/{source}/review");

    [Fact]
    public async Task SingleUploadWithTwoReceiptsRequiresSeparationAndPreservesCandidates()
    {
        await using var f = new Factory();
        await f.Seed();
        var text = "BON FISCAL NR A1\nTOTAL 10.00 RON\nBON FISCAL NR B2\nTOTAL 20.00 RON";
        var (r, d) = await Fixture(f, 1, text, "Single");
        var alice = await Login(f, "alice");
        var view = await View(alice, r, d);
        var review = view.GetProperty("review");
        var group = Assert.Single(review.GetProperty("candidates").EnumerateArray());
        Assert.Empty(group.GetProperty("fields").EnumerateObject());
        Assert.Empty(group.GetProperty("taxes").EnumerateArray());
        Assert.Contains(group.GetProperty("warnings").EnumerateArray(),
            w => w.GetString()!.StartsWith("MULTIPLE_SECTIONS_SINGLE:"));
        var version = review.GetProperty("version").GetInt64();
        Key(alice);
        var rejected = await alice.PostAsJsonAsync($"/api/reports/{r}/lines",
            new LineEdit(version, Data(), null, d, group.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        Key(alice);
        var expanded = await alice.PostAsJsonAsync(
            $"/api/reports/{r}/documents/{d}/groups",
            new GroupCommand(version, "expand", [group.GetProperty("id").GetGuid()],
                Reason: "Separate the two different fiscal receipts"));
        Assert.True(expanded.IsSuccessStatusCode, await expanded.Content.ReadAsStringAsync());
        view = await View(alice, r, d);
        Assert.Equal(2, view.GetProperty("review").GetProperty("candidates").GetArrayLength());
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        Assert.Equal("Multiple", (await db.ReportAttachments.SingleAsync()).ReceiptMode);
        Assert.Equal(3, await db.DocumentGroups.CountAsync());
        Assert.Equal("Split", (await db.DocumentGroups.SingleAsync(g => g.Id == group.GetProperty("id").GetGuid())).State);
    }

    [Fact]
    public async Task GroupsAreScopedVersionedAndSupportSplitMergeAndDisposition()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f);
        var alice = await Login(f, "alice");
        var bob = await Login(f, "bob");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.GetAsync($"/api/reports/{r}/documents/{d}/review")).StatusCode
        );
        var view = await View(alice, r, d);
        var ids = view.GetProperty("review")
            .GetProperty("candidates")
            .EnumerateArray()
            .Select(g => g.GetProperty("id").GetGuid())
            .ToArray();
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await alice.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(2, "merge", ids, Reason: "Same invoice pages")
                )
            ).StatusCode
        );
        view = await View(alice, r, d);
        var merged = Assert.Single(view.GetProperty("review").GetProperty("candidates").EnumerateArray());
        Assert.Equal(2, merged.GetProperty("pages").GetArrayLength());
        Key(alice);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await alice.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(
                        1,
                        "split",
                        [merged.GetProperty("id").GetGuid()],
                        Pages: [1],
                        Reason: "Separate receipts"
                    )
                )
            ).StatusCode
        );
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await alice.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(
                        3,
                        "split",
                        [merged.GetProperty("id").GetGuid()],
                        Pages: [1],
                        Reason: "Separate receipts"
                    )
                )
            ).StatusCode
        );
        view = await View(alice, r, d);
        var active = view.GetProperty("review").GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(2, active.Length);
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await alice.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(4, "delete", [active[0].GetProperty("id").GetGuid()], Reason: "Mistaken proposal")
                )
            ).StatusCode
        );
        view = await View(alice, r, d);
        Assert.Single(view.GetProperty("review").GetProperty("unassignedPages").EnumerateArray());
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await alice.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/pages",
                    new PageCommand(5, active[0].GetProperty("pages")[0].GetInt32(), true, "Supporting cover page")
                )
            ).StatusCode
        );
        view = await View(alice, r, d);
        Assert.Empty(view.GetProperty("review").GetProperty("unassignedPages").EnumerateArray());
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        Assert.Equal(5, await db.DocumentGroups.CountAsync());
        Assert.Equal(3, await db.DocumentGroups.CountAsync(g => g.State == "Merged" || g.State == "Split"));
        Assert.True(await db.Audits.CountAsync(a => a.Entity == d.ToString()) >= 4);
    }

    [Fact]
    public async Task VerificationCreatesOnlyOneExpenseAndRetryPreservesHumanCorrections()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        var alice = await Login(f, "alice");
        var view = await View(alice, r, d);
        var g = view.GetProperty("review").GetProperty("candidates")[0].GetProperty("id").GetGuid();
        Key(alice);
        var created = await alice.PostAsJsonAsync($"/api/reports/{r}/lines", new LineEdit(2, Data(), null, d, g));
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (await alice.PostAsJsonAsync($"/api/reports/{r}/lines", new LineEdit(3, Data(), null, d, g))).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            Assert.Single(await db.Lines.ToListAsync());
            var group = await db.DocumentGroups.SingleAsync();
            Assert.Equal("Verified", group.State);
            var expense = group.ExpenseId;
            var corrected = group.CorrectedExpense;
            var raw = (await db.DocumentReviews.SingleAsync()).OriginalExtraction;
            var retry = ReceiptParser.Parse([new(1, "INVOICE NO A1\nTOTAL 999.00 RON")], null);
            await OcrReviews.Import(db, r, d, retry);
            await db.SaveChangesAsync();
            Assert.Equal(expense, group.ExpenseId);
            Assert.Equal(corrected, group.CorrectedExpense);
            Assert.Equal(raw, (await db.DocumentReviews.SingleAsync()).OriginalExtraction);
            Assert.Equal(2, await db.ExtractionAttempts.CountAsync());
            var report = await new Reports(db).Get(
                r,
                new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [
                            new(
                                System.Security.Claims.ClaimTypes.NameIdentifier,
                                (await db.Users.SingleAsync(u => u.UserName == "alice")).Id
                            ),
                            new(System.Security.Claims.ClaimTypes.Role, "Employee"),
                        ],
                        "test"
                    )
                )
            );
            Assert.Empty(await OcrReviews.Problems(db, report));
        }
    }

    [Fact]
    public async Task FinanceRetainsGroupingRightsWhileEmployeeCannotEditFinancialReview()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            (await db.Reports.SingleAsync()).State = States.Finance;
            await db.SaveChangesAsync();
        }
        var finance = await Login(f, "finance");
        var alice = await Login(f, "alice");
        var view = await View(finance, r, d);
        var group = view.GetProperty("review").GetProperty("candidates")[0].GetProperty("id").GetGuid();
        var command = new GroupCommand(2, "regions", [group], [new(1)], Reason: "Review original page");
        Key(alice);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await alice.PostAsJsonAsync($"/api/reports/{r}/documents/{d}/groups", command)).StatusCode
        );
        Key(finance);
        Assert.Equal(
            HttpStatusCode.OK,
            (await finance.PostAsJsonAsync($"/api/reports/{r}/documents/{d}/groups", command)).StatusCode
        );
    }

    [Fact]
    public async Task MissingInvoiceNumberRollsBackVerificationAndLineCreation()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        Guid rate;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var vat = new Master
            {
                Kind = "vat",
                Code = "21",
                From = new(2020, 1, 1),
                Json = Serialization.Write(new { rate = 21 }),
            };
            db.Masters.Add(vat);
            await db.SaveChangesAsync();
            rate = vat.Id;
        }
        var alice = await Login(f, "alice");
        var view = await View(alice, r, d);
        var group = view.GetProperty("review").GetProperty("candidates")[0].GetProperty("id").GetGuid();
        var data = Data() with
        {
            Country = "RO",
            Cui = "18546294",
            HasCompanyCui = true,
            SupplierConfirmed = true,
            DocumentType = "InvoiceWithCui",
            Invoice = "",
            Amount = 121,
            Taxes = [new(rate, 100, 21)],
        };
        Key(alice);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await alice.PostAsJsonAsync($"/api/reports/{r}/lines", new LineEdit(2, data, null, d, group))).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            Assert.Empty(await db.Lines.ToListAsync());
            Assert.Null((await db.DocumentGroups.SingleAsync()).ExpenseId);
        }
    }

    [Fact]
    public async Task PagePreviewChecksScopeAndOriginalIntegrity()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a16kAAAAASUVORK5CYII="
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var source = await db.ReportAttachments.SingleAsync();
            db.Entry(source).State = EntityState.Detached;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ReportAttachments\" SET \"Mime\" = {"image/png"}, \"Hash\" = {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))} WHERE \"Id\" = {d}"
            );
            await scope.ServiceProvider.GetRequiredService<IStore>().Put("test", bytes, "image/png");
        }
        var alice = await Login(f, "alice");
        var bob = await Login(f, "bob");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.GetAsync($"/api/reports/{r}/documents/{d}/pages/1/preview")).StatusCode
        );
        var info = await alice.GetFromJsonAsync<JsonElement>($"/api/reports/{r}/documents/{d}/preview-info");
        Assert.Equal(1, info.GetProperty("pages").GetInt32());
        var response = await alice.GetAsync($"/api/reports/{r}/documents/{d}/pages/1/preview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await alice.GetAsync($"/api/reports/{r}/documents/{d}/pages/2/preview")).StatusCode
        );
    }

    [Fact]
    public async Task VatCorrectionsKeepOriginalPreviousAndVerifiedValues()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1, "INVOICE NO A1\n01.01.2026\nTVA 21% 100.00 21.00\nTOTAL 121.00 RON");
        Guid rate;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var master = new Master
            {
                Kind = "vat",
                Code = "21",
                From = new(2020, 1, 1),
                Json = Serialization.Write(new { rate = 21 }),
            };
            db.Masters.Add(master);
            await db.SaveChangesAsync();
            rate = master.Id;
        }
        var alice = await Login(f, "alice");
        var view = await View(alice, r, d);
        var group = view.GetProperty("review").GetProperty("candidates")[0].GetProperty("id").GetGuid();
        var input = Data() with
        {
            Country = "RO",
            DocumentType = "InvoiceWithCui",
            HasCompanyCui = true,
            Cui = "18546294",
            SupplierConfirmed = true,
            Amount = 12.10m,
            Taxes = [new(rate, 10, 2.10m)],
        };
        Key(alice);
        var result = await alice.PostAsJsonAsync($"/api/reports/{r}/lines", new LineEdit(2, input, null, d, group));
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        var line = (await result.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("lines")[0]
            .GetProperty("id")
            .GetGuid();
        var changed = input with { Amount = 12.16m, Taxes = [new(rate, 10.05m, 2.11m)] };
        Key(alice);
        result = await alice.PatchAsJsonAsync($"/api/reports/{r}/lines/{line}", new LineEdit(3, changed, null));
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        var feedback = await alice.GetFromJsonAsync<JsonElement>($"/api/reports/{r}/documents/{d}/feedback");
        Assert.Equal(2, feedback.GetArrayLength());
        var first = feedback[0]
            .GetProperty("fields")
            .EnumerateArray()
            .Single(v => v.GetProperty("field").GetString() == "vat.21.vat");
        var second = feedback[1]
            .GetProperty("fields")
            .EnumerateArray()
            .Single(v => v.GetProperty("field").GetString() == "vat.21.vat");
        decimal Amount(JsonElement row, string field) =>
            decimal.Parse(row.GetProperty(field).GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(21m, Amount(first, "raw"));
        Assert.Equal(2.10m, Amount(first, "verified"));
        Assert.Equal(21m, Amount(second, "raw"));
        Assert.Equal(2.10m, Amount(second, "previous"));
        Assert.Equal(2.11m, Amount(second, "verified"));
        var bob = await Login(f, "bob");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.GetAsync($"/api/reports/{r}/documents/{d}/feedback")).StatusCode
        );
    }

    [Fact]
    public async Task CapturedBatchCannotBypassReviewByConfirmingSharedCoverage()
    {
        await using var f = new Factory();
        await f.Seed();
        Guid id;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var alice = await db.Users.SingleAsync(u => u.UserName == "alice");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var line = new Line
            {
                Position = 1,
                EstimatedRon = 10,
                Json = Serialization.Write(Data()),
            };
            var report = new Report
            {
                EmployeeId = alice.Id,
                ManagerId = manager.Id,
                Purpose = "Capture review gate",
                Lines = [line],
            };
            report.Attachments.Add(
                new()
                {
                    ReportId = report.Id,
                    Capture = true,
                    Scan = "Clean",
                    CoveredLines = Serialization.Write(new[] { line.Id }),
                    Ocr = Serialization.Write(new { state = "Pending" }),
                }
            );
            db.Reports.Add(report);
            db.Masters.AddRange(
                new Master
                {
                    Kind = "expenseTypes",
                    Code = "GENERAL",
                    From = new(2020, 1, 1),
                },
                new Master
                {
                    Kind = "costCenters",
                    Code = "CC",
                    From = new(2020, 1, 1),
                }
            );
            await db.SaveChangesAsync();
            id = report.Id;
        }
        var client = await Login(f, "alice");
        Key(client);
        var response = await client.PostAsJsonAsync($"/api/reports/{id}/submit", new VersionRequest(1, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ocr_review_required", await response.Content.ReadAsStringAsync());
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            Assert.Empty(await db.Snapshots.ToListAsync());
            Assert.Equal(States.Draft, (await db.Reports.SingleAsync()).State);
        }
    }

    [Fact]
    public async Task ManualReviewAfterOcrFailureAccountsForActualImagePage()
    {
        await using var f = new Factory();
        await f.Seed();
        Guid id,
            doc;
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a16kAAAAASUVORK5CYII="
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var alice = await db.Users.SingleAsync(u => u.UserName == "alice");
            var report = new Report { EmployeeId = alice.Id, Purpose = "Manual source review" };
            var file = new ReportAttachment
            {
                ReportId = report.Id,
                Capture = true,
                Key = "manual-source",
                Mime = "image/png",
                Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
                Scan = "Clean",
                Ocr = Serialization.Write(new { state = "Failed" }),
            };
            report.Attachments.Add(file);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IStore>().Put(file.Key, bytes, file.Mime);
            id = report.Id;
            doc = file.Id;
        }
        var client = await Login(f, "alice");
        Key(client);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await client.PostAsJsonAsync(
                    $"/api/reports/{id}/documents/{doc}/review/start",
                    new VersionRequest(1, null)
                )
            ).StatusCode
        );
        Key(client);
        var started = await client.PostAsJsonAsync(
            $"/api/reports/{id}/documents/{doc}/review/start",
            new VersionRequest(1, "Manual review after OCR outage")
        );
        Assert.True(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        var view = await View(client, id, doc);
        Assert.True(view.GetProperty("available").GetBoolean());
        Assert.Equal(1, view.GetProperty("review").GetProperty("pageCount").GetInt32());
        Assert.Single(view.GetProperty("review").GetProperty("unassignedPages").EnumerateArray());
    }

    [Fact]
    public async Task ExtractionEvidenceIsAppendOnly()
    {
        await using var f = new Factory();
        await f.Seed();
        await Fixture(f, 1);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        (await db.ExtractionAttempts.SingleAsync()).Json = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void SharedPageRegionsMustNotOverlap()
    {
        Assert.False(OcrReviews.Overlap(new(1, 0, 0, .5m, 1), new(1, .5m, 0, .5m, 1)));
        Assert.True(OcrReviews.Overlap(new(1), new(1, .5m, 0, .5m, 1)));
        Assert.Throws<DomainException>(() => OcrReviews.ValidateRegions([new(1, .8m, 0, .5m, 1)], 1));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(19)]
    public void DisabledRatesCannotBeOverriddenOrPosted(int percent)
    {
        var rate = new Master
        {
            Kind = "vat",
            Code = percent.ToString(),
            From = new(2020, 1, 1),
            Json = Serialization.Write(new { rate = percent }),
        };
        var data = Data() with
        {
            Country = "RO",
            HasCompanyCui = true,
            Amount = 100 + percent,
            Taxes = [new(rate.Id, 100, percent)],
        };
        var line = new Line
        {
            Json = Serialization.Write(data),
            EstimatedRon = data.Amount,
            VatOverrideReason = "Finance checked",
        };
        Assert.Contains(Rules.VatIssues(line, [rate]), p => p.Code == "vat_rate" && p.Blocking);
        Assert.Equal(
            "vat_rate",
            Assert
                .Throws<DomainException>(() =>
                    new Posting().Build(new() { Lines = [line] }, new() { Worker = "employee" }, [rate])
                )
                .Code
        );
    }

    [Fact]
    public async Task SharedDraftCorrectionIsVersionedUnverifiedAndPreservesRawEvidence()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        var alice = await Login(f, "alice");
        var bob = await Login(f, "bob");
        var view = await View(alice, r, d);
        var g = view.GetProperty("review").GetProperty("candidates")[0].GetProperty("id").GetGuid();
        var command = new OcrDraftCommand(2, Data() with { Amount = 12, Description = "Human draft" });
        Key(bob);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.PatchAsJsonAsync($"/api/reports/{r}/documents/{d}/groups/{g}/fields", command)).StatusCode
        );
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (await alice.PatchAsJsonAsync($"/api/reports/{r}/documents/{d}/groups/{g}/fields", command)).StatusCode
        );
        Key(alice);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await alice.PatchAsJsonAsync($"/api/reports/{r}/documents/{d}/groups/{g}/fields", command)).StatusCode
        );
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var group = await db.DocumentGroups.SingleAsync();
        Assert.Equal("ReviewRequired", group.State);
        Assert.Null(group.ExpenseId);
        Assert.Empty(await db.Lines.ToListAsync());
        Assert.Equal("10.00", Serialization.Read<Dictionary<string, OcrField>>(group.Fields)["amount"].Value);
        Assert.Equal(12, Serialization.Read<ExpenseData>(group.CorrectedExpense!).Amount);
        Assert.True(await db.Audits.AnyAsync(a => a.Action == "OcrDraftCorrected"));
        Assert.True(await db.DocumentEvents.AnyAsync(e => e.Type == "HumanCorrectionApplied"));
    }

    [Fact]
    public async Task PreviewTokensAreUserBoundAndStorageKeysAreNeverExposed()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        var alice = await Login(f, "alice");
        var response = await alice.GetFromJsonAsync<JsonElement>($"/api/reports/{r}");
        Assert.False(response.GetProperty("report").GetProperty("attachments")[0].TryGetProperty("key", out _));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await alice.GetAsync($"/api/reports/{r}/documents/{d}/pages/1/preview?viewer=invalid-token")).StatusCode
        );
    }

    [Fact]
    public async Task RegroupedDraftsCannotKeepFieldsFromOutsideTheirSourceRegion()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 2);
        var c = await Login(f, "alice");
        var first = (await View(c, r, d))
            .GetProperty("review")
            .GetProperty("candidates")[0]
            .GetProperty("id")
            .GetGuid();
        Key(c);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(2, "regions", [first], [new(2)], Reason: "Move to the second source page")
                )
            ).StatusCode
        );
        var groups = (await View(c, r, d)).GetProperty("review").GetProperty("candidates").EnumerateArray();
        Assert.Equal(
            "A2",
            groups
                .Single(g => g.GetProperty("id").GetGuid() == first)
                .GetProperty("fields")
                .GetProperty("invoice")
                .GetProperty("value")
                .GetString()
        );
        Key(c);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync(
                    $"/api/reports/{r}/documents/{d}/groups",
                    new GroupCommand(
                        3,
                        "regions",
                        [first],
                        [new(2, 0, 0, .25m, .25m)],
                        Reason: "Tight crop without available field boxes"
                    )
                )
            ).StatusCode
        );
        var cropped = (await View(c, r, d))
            .GetProperty("review")
            .GetProperty("candidates")
            .EnumerateArray()
            .Single(g => g.GetProperty("id").GetGuid() == first);
        Assert.Empty(cropped.GetProperty("fields").EnumerateObject());
    }

    [Fact]
    public async Task AdministrativeOcrRetryPreservesTheFailedJobAndAttemptHistory()
    {
        await using var f = new Factory();
        await f.Seed();
        var (r, d) = await Fixture(f, 1);
        Guid job;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var failed = new Job
            {
                Kind = "OcrReport",
                Json = d.ToString(),
                State = "Failed",
                Attempts = 5,
                FailureCode = "processing_failed",
                CompletedAt = DateTime.UtcNow,
            };
            db.Jobs.Add(failed);
            await db.SaveChangesAsync();
            job = failed.Id;
        }
        var admin = await Login(f, "admin");
        Key(admin);
        var response = await admin.PostAsJsonAsync(
            $"/api/admin/jobs/{job}/retry",
            new ReasonInput("Retry after provider recovery")
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(job, next);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var old = await db.Jobs.SingleAsync(j => j.Id == job);
            Assert.Equal(5, old.Attempts);
            Assert.Equal("Retried", old.State);
            Assert.NotNull(old.CompletedAt);
            Assert.Equal("Pending", (await db.Jobs.SingleAsync(j => j.Id == next)).State);
        }
        Key(admin);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await admin.PostAsJsonAsync($"/api/admin/jobs/{job}/retry", new ReasonInput("Duplicate retry"))).StatusCode
        );
    }
}
