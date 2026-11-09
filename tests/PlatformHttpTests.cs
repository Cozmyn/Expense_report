using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Decont;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Decont.Tests;

public class PlatformHttpTests
{
    static async Task<HttpClient> Login(Factory f, string username)
    {
        var c = f.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var result = await c.PostAsJsonAsync("/api/auth/login", new { username, password = "Valid-test-Password1!" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
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

    [Fact]
    public async Task PasswordRecoveryTokenIsSingleUseAndRevokesActiveSessions()
    {
        await using var f = new Factory();
        await f.Seed();
        var alice = await Login(f, "alice");
        string token,
            id;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await users.FindByNameAsync("alice");
            id = user!.Id;
            token = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user))
            );
        }
        var anonymous = f.CreateClient(new() { HandleCookies = true });
        var csrf = await anonymous.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        anonymous.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var input = new
        {
            userId = id,
            token,
            password = "New-recovery-Password2!",
        };
        Assert.Equal(
            HttpStatusCode.OK,
            (await anonymous.PostAsJsonAsync("/api/auth/reset-password", input)).StatusCode
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await anonymous.PostAsJsonAsync("/api/auth/reset-password", input)).StatusCode
        );
    }

    [Fact]
    public async Task PasswordRecoveryRejectsExpiredTokens()
    {
        await using var f = new Factory();
        await f.Seed();
        string token,
            id;
        using (var scope = f.Services.CreateScope())
        {
            scope
                .ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>()
                .Value.TokenLifespan = TimeSpan.FromSeconds(-1);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await users.FindByNameAsync("alice");
            id = user!.Id;
            token = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user))
            );
        }
        var client = f.CreateClient();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await client.PostAsJsonAsync(
                    "/api/auth/reset-password",
                    new
                    {
                        userId = id,
                        token,
                        password = "New-recovery-Password2!",
                    }
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task SessionRefreshesWithActivityAndExpiresAfterInactivity()
    {
        await using var f = new Factory();
        await f.Seed();
        var client = await Login(f, "alice");
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var session = await db.Sessions.SingleAsync();
            session.Expires = DateTime.UtcNow.AddSeconds(10);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var session = await db.Sessions.SingleAsync();
            Assert.True(session.Expires > DateTime.UtcNow.AddMinutes(20));
            session.Expires = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task MasterImportIsAtomicAndPreferencesAreUserScoped()
    {
        await using var f = new Factory();
        await f.Seed();
        var finance = await Login(f, "finance");
        Key(finance);
        var rows = new[]
        {
            new MasterInput("costCenters", "NEW", "Nou", "New", "{}", new(2026, 1, 1), null, true, "Preview"),
            new MasterInput(
                "costCenters",
                "NEW",
                "Duplicat",
                "Duplicate",
                "{}",
                new(2026, 2, 1),
                null,
                true,
                "Preview"
            ),
        };
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await finance.PostAsJsonAsync(
                    "/api/configuration/import/confirm",
                    new MasterBatch(rows.ToList(), "Approved import")
                )
            ).StatusCode
        );
        using (var scope = f.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<Db>().Masters.AnyAsync(m => m.Code == "NEW"));
        var alice = await Login(f, "alice");
        Key(alice);
        Assert.Equal(
            HttpStatusCode.OK,
            (await alice.PutAsJsonAsync("/api/preferences/finance-filters", new { q = "Private filter" })).StatusCode
        );
        var bob = await Login(f, "bob");
        Assert.Equal("{}", await bob.GetStringAsync("/api/preferences/finance-filters"));
    }

    [Fact]
    public void SpreadsheetRejectsFormulasAndPreservesExcelDates()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");
        sheet.Cell(1, 1).Value = "date";
        sheet.Cell(2, 1).Value = new DateTime(2026, 1, 1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        Assert.Equal("2026-01-01", SpreadsheetImports.Read("statement.xlsx", stream.ToArray())[1][0]);
        sheet.Cell(2, 1).FormulaA1 = "1+1";
        using var invalid = new MemoryStream();
        workbook.SaveAs(invalid);
        Assert.Throws<DomainException>(() => SpreadsheetImports.Read("statement.xlsx", invalid.ToArray()));
    }

    [Fact]
    public async Task CaptureCanPrecedeLinesButCannotBeUsedBeforeScanningAndUploadIsIdempotent()
    {
        await using var f = new Factory();
        await f.Seed();
        var client = await Login(f, "alice");
        Key(client);
        var created = await client.PostAsJsonAsync("/api/reports", new { purpose = "OCR upload first" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Key(client);
        async Task<HttpResponseMessage> Upload()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("1"), "version");
            form.Add(
                new ByteArrayContent(
                    Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a16kAAAAASUVORK5CYII="
                    )
                ),
                "file",
                "receipt.png"
            );
            return await client.PostAsync($"/api/reports/{id}/capture", form);
        }
        var first = await Upload();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await (await Upload()).Content.ReadAsStringAsync());
        var original = (await first.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("e")
            .GetProperty("id")
            .GetGuid();
        Key(client);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await client.PostAsJsonAsync(
                    $"/api/reports/{id}/documents/{original}/extract",
                    new VersionRequest(2, null)
                )
            ).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            Assert.Single(await db.ReportAttachments.ToListAsync());
            Assert.Equal("[]", (await db.ReportAttachments.SingleAsync()).CoveredLines);
        }
    }

    [Fact]
    public async Task SchemaUpgradeIsRepeatableAndDatabaseProtectsAuditEvidence()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.Apply(db);
        db.Audits.Add(
            new()
            {
                Actor = "Test",
                Entity = "Test",
                Action = "AppendOnly",
            }
        );
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.SchemaReleases.CountAsync(r => r.Id == 3));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"Audits\""));
        Assert.True(await db.Audits.AnyAsync(a => a.Action == "AppendOnly"));
    }

    [Fact]
    public async Task ReopeningPostedBatchRequiresSpecialPermissionAndReversalReference()
    {
        await using var f = new Factory();
        await f.Seed();
        var finance = await Login(f, "finance");
        Guid batchId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var r = new Report
            {
                EmployeeId = employee.Id,
                ManagerId = manager.Id,
                State = States.Posted,
                Purpose = "Reversal test",
            };
            var batch = new Batch
            {
                ReportId = r.Id,
                Revision = 1,
                State = "Posted",
                Rows = "[]",
                Hash = "immutable",
                FileName = "immutable.xlsx",
                Reference = "ERP-ORIGINAL",
            };
            db.Reports.Add(r);
            db.Batches.Add(batch);
            await db.SaveChangesAsync();
            batchId = batch.Id;
        }
        Key(finance);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await finance.PostAsJsonAsync(
                    $"/api/finance/batches/{batchId}/reopen",
                    new ReopenInput(1, "Correction reason", "ERP-REVERSAL")
                )
            ).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.AddToRoleAsync((await users.FindByNameAsync("finance"))!, "FinanceReopen");
        }
        finance = await Login(f, "finance");
        Key(finance);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await finance.PostAsJsonAsync(
                    $"/api/finance/batches/{batchId}/reopen",
                    new ReopenInput(1, "Correction reason", "")
                )
            ).StatusCode
        );
        Key(finance);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await finance.PostAsJsonAsync(
                    $"/api/finance/batches/{batchId}/reopen",
                    new ReopenInput(1, "Correction reason", "ERP-REVERSAL")
                )
            ).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var batch = await db.Batches.FindAsync(batchId);
            Assert.Equal("Reversed", batch!.State);
            Assert.Equal("ERP-ORIGINAL", batch.Reference);
            Assert.Equal("immutable.xlsx", batch.FileName);
            Assert.Equal(States.Reapproval, (await db.Reports.FindAsync(batch.ReportId))!.State);
            Assert.Single(await db.Snapshots.ToListAsync());
        }
    }

    [Fact]
    public async Task MultiRoleManagerInboxContainsOnlyRoutedApprovals()
    {
        await using var f = new Factory();
        await f.Seed();
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var manager = (await users.FindByNameAsync("manager"))!;
            await users.AddToRoleAsync(manager, "Finance");
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var employee = (await users.FindByNameAsync("alice"))!;
            var other = (await users.FindByNameAsync("admin"))!;
            db.Reports.Add(
                new()
                {
                    EmployeeId = employee.Id,
                    ManagerId = manager.Id,
                    State = States.Manager,
                    Purpose = "Routed to me",
                }
            );
            db.Reports.Add(
                new()
                {
                    EmployeeId = employee.Id,
                    ManagerId = other.Id,
                    State = States.Manager,
                    Purpose = "Routed elsewhere",
                }
            );
            await db.SaveChangesAsync();
        }
        var client = await Login(f, "manager");
        var reports = await client.GetFromJsonAsync<JsonElement>("/api/reports");
        Assert.Equal(2, reports.GetArrayLength());
        Assert.True(
            reports
                .EnumerateArray()
                .Single(r => r.GetProperty("purpose").GetString() == "Routed to me")
                .GetProperty("canApprove")
                .GetBoolean()
        );
        Assert.False(
            reports
                .EnumerateArray()
                .Single(r => r.GetProperty("purpose").GetString() == "Routed elsewhere")
                .GetProperty("canApprove")
                .GetBoolean()
        );
    }

    [Fact]
    public async Task ManagerCannotApproveAReportWithBlockingValidationErrors()
    {
        await using var f = new Factory();
        await f.Seed();
        Guid id;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var r = new Report
            {
                EmployeeId = employee.Id,
                ManagerId = manager.Id,
                State = States.Manager,
                Purpose = "Invalid submitted report",
                Revision = 1,
            };
            db.Reports.Add(r);
            db.Snapshots.Add(
                new()
                {
                    ReportId = r.Id,
                    Revision = 1,
                    Json = "{}",
                    Hash = "retained",
                }
            );
            await db.SaveChangesAsync();
            id = r.Id;
        }
        var client = await Login(f, "manager");
        Key(client);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"/api/reports/{id}/actions/approve", new VersionRequest(1, null))).StatusCode
        );
        using (var scope = f.Services.CreateScope())
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<Db>().Decisions.ToListAsync());
    }

    [Fact]
    public async Task FinanceRoutingCorrectionIsAuditedAndRequiresFreshApproval()
    {
        await using var f = new Factory();
        await f.Seed();
        Guid id;
        string employeeId,
            managerId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var alice = await db.Users.SingleAsync(u => u.UserName == "alice");
            var bob = await db.Users.SingleAsync(u => u.UserName == "bob");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var r = new Report
            {
                EmployeeId = alice.Id,
                ManagerId = manager.Id,
                State = States.Finance,
                Purpose = "Employee correction",
            };
            db.Reports.Add(r);
            await db.SaveChangesAsync();
            id = r.Id;
            employeeId = bob.Id;
            managerId = manager.Id;
        }
        var aliceClient = await Login(f, "alice");
        Key(aliceClient);
        var input = new RoutingInput(1, employeeId, managerId, "Wrong expense employee");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await aliceClient.PostAsJsonAsync($"/api/finance/reports/{id}/routing", input)).StatusCode
        );
        var finance = await Login(f, "finance");
        Key(finance);
        Assert.Equal(
            HttpStatusCode.OK,
            (await finance.PostAsJsonAsync($"/api/finance/reports/{id}/routing", input)).StatusCode
        );
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var report = await db.Reports.FindAsync(id);
            Assert.Equal(employeeId, report!.EmployeeId);
            Assert.Equal(States.Reapproval, report.State);
            Assert.Equal(1, report.Revision);
            Assert.Single(await db.Snapshots.ToListAsync());
            var audit = await db.Audits.SingleAsync(a => a.Action == "ReportRoutingCorrected");
            Assert.Equal(input.Reason, audit.Reason);
            Assert.Contains(employeeId, audit.After!);
        }
    }

    [Fact]
    public async Task ExistingSupplierWithoutAccountCanBeRegisteredForItsFirstReceiptDate()
    {
        await using var f = new Factory();
        await f.Seed();
        Guid requestId,
            reportId;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var employee = await db.Users.SingleAsync(u => u.UserName == "alice");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var vat = new Master
            {
                Kind = "vat",
                Code = "ZERO",
                From = new(2020, 1, 1),
                Json = "{\"rate\":0,\"salesTaxGroup\":\"VDOM\",\"itemTaxGroup\":\"G0\"}",
            };
            var data = new ExpenseData(
                "Known supplier",
                "18546294",
                "FIRST",
                "2026-01-01",
                "RO",
                "Receipt",
                "GENERAL",
                "Receipt",
                "Personal",
                null,
                "CC",
                null,
                100,
                "RON",
                [new(vat.Id, 100, 0)],
                true
            );
            var r = new Report
            {
                EmployeeId = employee.Id,
                ManagerId = manager.Id,
                State = States.Finance,
                Purpose = "Missing existing supplier account",
                Lines =
                [
                    new()
                    {
                        Position = 1,
                        EstimatedRon = 100,
                        Json = Serialization.Write(data),
                    },
                ],
            };
            db.Reports.Add(r);
            db.Masters.AddRange(
                vat,
                new Master
                {
                    Kind = "suppliers",
                    Code = "18546294",
                    NameRo = "Known supplier",
                    From = new(2026, 1, 1),
                    Json = "{}",
                },
                new Master
                {
                    Kind = "accounting",
                    Code = "GENERAL",
                    From = new(2020, 1, 1),
                    Json = "{\"mainAccount\":\"GL\",\"localAccount\":\"LOCAL\"}",
                },
                new Master
                {
                    Kind = "costCenters",
                    Code = "CC",
                    From = new(2020, 1, 1),
                    Json = "{\"erpCode\":\"CC\"}",
                },
                new Master
                {
                    Kind = "erpSettings",
                    Code = "default",
                    From = new(2020, 1, 1),
                    Json = "{\"approvalReference\":\"Test\",\"clearingAccount\":\"CLEAR\",\"clearingLocal\":\"LOCAL\"}",
                }
            );
            var request = new VendorRequest
            {
                ReportId = r.Id,
                Cui = "18546294",
                Supplier = "Known supplier",
            };
            db.Vendors.Add(request);
            await db.SaveChangesAsync();
            reportId = r.Id;
            requestId = request.Id;
        }
        var finance = await Login(f, "finance");
        Key(finance);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (
                await finance.PostAsJsonAsync(
                    $"/api/finance/vendors/{requestId}/resolve",
                    new VendorInput("ERP-REGISTERED", new(2026, 1, 2), "Registered vendor")
                )
            ).StatusCode
        );
        Key(finance);
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await finance.PostAsJsonAsync(
                    $"/api/finance/vendors/{requestId}/resolve",
                    new VendorInput("ERP-REGISTERED", new(2026, 1, 1), "Registered vendor")
                )
            ).StatusCode
        );
        var lookup = await finance.GetFromJsonAsync<JsonElement>("/api/suppliers/lookup?cui=18546294&date=2026-01-01");
        Assert.True(lookup.GetProperty("hasErpAccount").GetBoolean());
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var supplier = await db.Masters.SingleAsync(m => m.Kind == "suppliers");
            Assert.Equal("{}", supplier.Json);
            var r = await db.Reports.Include(r => r.Lines).SingleAsync(r => r.Id == reportId);
            var output = new Posting().Build(
                r,
                (await db.Users.FindAsync(r.EmployeeId))!,
                await db.Masters.ToListAsync(),
                await db.SupplierAccounts.ToListAsync()
            );
            Assert.Contains(output.Rows, row => row[1] == "Vendor" && row[2] == "ERP-REGISTERED");
        }
    }
}
