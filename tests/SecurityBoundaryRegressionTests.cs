using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

/// <summary>
/// Security boundaries deliberately tested using the public HTTP pipeline.
/// These tests characterize existing behavior, not a change to authorization policy.
/// </summary>
public class SecurityBoundaryRegressionTests
{
    static async Task<HttpClient> Login(Factory factory, string username)
    {
        var client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "Valid-test-Password1!" }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    static void NewWriteKey(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/reports")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/finance/batches")]
    public async Task SensitiveEndpointsRequireAuthenticatedSession(string route)
    {
        await using var factory = new Factory();
        await factory.Seed();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task LoginWithoutCsrfDoesNotCreateAuditEventOrSession()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "alice", password = "Valid-test-Password1!" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        Assert.Empty(await db.Sessions.ToListAsync());
        Assert.Empty(await db.Audits.ToListAsync());
    }

    [Fact]
    public async Task AuthenticatedWriteWithoutCsrfCannotCreateAReport()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var alice = await Login(factory, "alice");
        NewWriteKey(alice);
        alice.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var response = await alice.PostAsJsonAsync("/api/reports", new { purpose = "Must not exist" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<Db>().Reports.ToListAsync());
    }

    [Fact]
    public async Task ATokenFromAnotherAuthenticatedBrowserCannotAuthorizeAWrite()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var alice = await Login(factory, "alice");
        var bob = await Login(factory, "bob");
        var aliceToken = await alice.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        bob.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        bob.DefaultRequestHeaders.Add("X-CSRF-TOKEN", aliceToken.GetProperty("token").GetString());
        NewWriteKey(bob);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await bob.PostAsJsonAsync("/api/reports", new { purpose = "Forbidden" })).StatusCode
        );
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<Db>().Reports.ToListAsync());
    }

    [Fact]
    public async Task AuthenticatedWriteWithoutIdempotencyKeyDoesNotPersist()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var alice = await Login(factory, "alice");
        var response = await alice.PostAsJsonAsync("/api/reports", new { purpose = "No operation key" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<Db>().Reports.ToListAsync());
    }

    [Fact]
    public async Task FinanceAndAdministrationRemainSeparatePermissionBoundaries()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var employee = await Login(factory, "alice");
        var finance = await Login(factory, "finance");
        var admin = await Login(factory, "admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/finance/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/finance/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync("/api/finance/batches")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task AnotherEmployeeCannotReadOrChangeAReportOrItsVersion()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var alice = await Login(factory, "alice");
        var bob = await Login(factory, "bob");
        NewWriteKey(alice);
        var created = await alice.PostAsJsonAsync("/api/reports", new { purpose = "Owner's private trip" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        var version = original.GetProperty("version").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/reports/{id}")).StatusCode);
        NewWriteKey(bob);
        var unauthorized = await bob.PatchAsJsonAsync(
            $"/api/reports/{id}",
            new { version, purpose = "Intruder change" }
        );
        Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Db>().Reports.SingleAsync(x => x.Id == id);
        Assert.Equal("Owner's private trip", stored.Purpose);
        Assert.Equal(version, stored.Version);
    }

    [Fact]
    public async Task RoleChangeRevokesTheOldSessionWithoutDeactivatingTheAccount()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var finance = await Login(factory, "finance");
        var admin = await Login(factory, "admin");
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync("/api/finance/batches")).StatusCode);
        var identity = await finance.GetFromJsonAsync<JsonElement>("/api/me");
        var financeId = identity.GetProperty("id").GetString();

        NewWriteKey(admin);
        var changed = await admin.PutAsJsonAsync(
            "/api/admin/users/" + financeId,
            new UserInput(
                "finance", "finance", "finance@example.invalid", "finance", null,
                true, ["Employee"], null, "Role reduction regression test"
            )
        );
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await finance.GetAsync("/api/me")).StatusCode);

        var freshSession = await Login(factory, "finance");
        var current = await freshSession.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Contains(current.GetProperty("roles").EnumerateArray(),
            item => item.GetString() == "Employee");
        Assert.DoesNotContain(current.GetProperty("roles").EnumerateArray(),
            item => item.GetString() == "Finance");
        Assert.Equal(HttpStatusCode.Forbidden, (await freshSession.GetAsync("/api/finance/batches")).StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesOnlyTheCurrentServerSideSession()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var first = await Login(factory, "alice");
        var second = await Login(factory, "alice");
        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<Db>().Sessions.CountAsync());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task SessionCookieIsHttpOnlyAndSameSiteStrict()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "alice", password = "Valid-test-Password1!" }
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var sessionCookie = Assert.Single(cookies!, x => x.StartsWith("decont.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiResponsesAreNeverCachedAndUseDefensiveHeaders()
    {
        await using var factory = new Factory();
        await factory.Seed();
        var alice = await Login(factory, "alice");
        var result = await alice.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", result.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", result.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(string.IsNullOrWhiteSpace(result.Headers.GetValues("X-Request-ID").Single()));
    }
}
