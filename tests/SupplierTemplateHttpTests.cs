using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class SupplierTemplateHttpTests
{
    private static async Task<HttpClient> Finance(Factory f)
    {
        var client = f.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new {
                username = "finance", password = "Valid-test-Password1!"
            })).StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    private static void NewKey(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task FinanceCanVersionAndDisableOneTrustedLayoutWithoutDeletingPreviousVersion()
    {
        await using var f = new Factory();
        await f.Seed();
        List<Guid> documents = [];
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            var alice = await db.Users.SingleAsync(u => u.UserName == "alice");
            var manager = await db.Users.SingleAsync(u => u.UserName == "manager");
            var report = new Report { EmployeeId = alice.Id, ManagerId = manager.Id,
                Purpose = "Supplier OCR template test" };
            var expense = new ExpenseData("Vendor", "18546294", "R-99",
                "2026-11-11", "RO", "InvoiceWithCui", "GENERAL",
                "Business expense", "Personal", null, "CC", null,
                121m, "RON", []);
            for (var i = 0; i < 2; i++)
            {
                var file = new ReportAttachment {
                    ReportId = report.Id, Name = "verified-" + i + ".pdf",
                    Mime = "application/pdf", Key = "test-" + i, Scan = "Clean",
                    Hash = Guid.NewGuid().ToString("N")
                };
                report.Attachments.Add(file);
                db.DocumentReviews.Add(new DocumentReview { Id = file.Id,
                    ReportId = report.Id, PageCount = 1 });
                db.DocumentGroups.Add(new DocumentGroup {
                    BundleId = file.Id, State = "Verified",
                    VerifiedAt = DateTime.UtcNow,
                    CorrectedExpense = Serialization.Write(expense),
                    Regions = Serialization.Write(new[] { new SourceRegion(1) }),
                    Fields = Serialization.Write(new Dictionary<string, OcrField> {
                        ["amount"] = new("121.00", 1, "TOTAL 121.00",
                            Source: "HumanSelected",
                            Box: new SourceBox(.1m, .72m, .5m, .04m))
                    })
                });
                documents.Add(file.Id);
            }
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }
        var client = await Finance(f);
        var list = await client.GetFromJsonAsync<JsonElement>(
            "/api/finance/supplier-ocr-templates/examples?cui=18546294");
        Assert.Equal(2, list.GetArrayLength());

        NewKey(client);
        var created = await client.PostAsJsonAsync("/api/finance/supplier-ocr-templates",
            new { cui = "18546294", layoutId = "branch-1", name = "Till one",
                verifiedGroups = documents, reason = "Two verified originals" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(first.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, first.GetProperty("revision").GetInt32());
        Assert.Equal(2, first.GetProperty("examples").GetArrayLength());

        NewKey(client);
        var disabled = await client.PostAsJsonAsync(
            "/api/finance/supplier-ocr-templates/18546294/branch-1/toggle?enabled=false",
            new { reason = "Till layout is no longer used" });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var after = await disabled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(after.GetProperty("enabled").GetBoolean());
        Assert.Equal(2, after.GetProperty("revision").GetInt32());

        using var verify = f.Services.CreateScope();
        var db2 = verify.ServiceProvider.GetRequiredService<Db>();
        var history = await db2.Masters.Where(m => m.Kind == SupplierOcrTemplates.Kind)
            .OrderBy(m => m.Code).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.True(history[0].Active); // Historic active revision is immutable.
        Assert.False(history[1].Active);
        var latest = await client.GetFromJsonAsync<JsonElement>(
            "/api/finance/supplier-ocr-templates?cui=18546294");
        Assert.False(Assert.Single(latest.EnumerateArray()).GetProperty("enabled").GetBoolean());
    }
}
