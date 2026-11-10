using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class ImportTemplateHttpTests
{
    private static async Task<HttpClient> Login(Factory factory, string username)
    {
        var client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username, password = "Valid-test-Password1!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    [Fact]
    public async Task SamplesAreDownloadableOnlyThroughPermittedRoleMenus()
    {
        await using var f = new Factory();
        await f.Seed();
        using var admin = await Login(f, "admin");
        using var finance = await Login(f, "finance");
        using var employee = await Login(f, "alice");

        foreach (var (url, expected) in new[]
        {
            ("/api/admin/employee-import/template/managers", "approvalMode"),
            ("/api/admin/cost-center-owners/template", "managerUsername"),
            ("/api/configuration/import/template/costCenters", "erpCode"),
            ("/api/configuration/import/template/cards", "lastFour"),
        })
        {
            var response = await admin.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var contents = await response.Content.ReadAsStringAsync();
            Assert.Contains(expected, contents);
            Assert.Contains("text/csv", response.Content.Headers.ContentType?.ToString());
            Assert.NotEqual(HttpStatusCode.OK, (await employee.GetAsync(url)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await finance.GetAsync("/api/admin/employee-import/template/managers")).StatusCode);
        var bank = await finance.GetAsync("/api/finance/transactions/template");
        Assert.Equal(HttpStatusCode.OK, bank.StatusCode);
        Assert.Contains("settledRon", await bank.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdminCanSetFixedApprovalButNotAnInvalidNamedManager()
    {
        await using var f = new Factory();
        await f.Seed();
        string managerId;
        using (var scope = f.Services.CreateScope())
        {
            managerId = (await scope.ServiceProvider.GetRequiredService<Db>().Users
                .SingleAsync(u => u.UserName == "manager")).Id;
        }
        using var admin = await Login(f, "admin");
        void NewKey()
        {
            admin.DefaultRequestHeaders.Remove("Idempotency-Key");
            admin.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }
        var named = new UserInput(
            "special-person", "Special Person", "special@example.invalid",
            "SPECIAL-001", null, true, ["Employee"],
            "Valid-test-Password1!", "Named exception", null, "Fixed", managerId);
        NewKey();
        var created = await admin.PostAsJsonAsync("/api/admin/users", named);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using (var scope = f.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<Db>().Users
                .SingleAsync(u => u.UserName == named.Username);
            Assert.Equal("Fixed", user.ApprovalMode);
            Assert.Equal(managerId, user.FixedApproverId);
        }

        var invalid = named with
        {
            Username = "invalid-special",
            Email = "invalid-special@example.invalid",
            FixedApproverId = "not-a-manager"
        };
        NewKey();
        var response = await admin.PostAsJsonAsync("/api/admin/users", invalid);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
