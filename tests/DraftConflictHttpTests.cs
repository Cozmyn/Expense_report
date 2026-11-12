using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Decont;
using Xunit;

namespace Decont.Tests;

public class DraftConflictHttpTests
{
    private static async Task<HttpClient> Login(Factory f, string user)
    {
        var client = f.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = user, password = "Valid-test-Password1!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    private static void UniqueKey(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task TwoTabsCannotSilentlyOverwriteEachOthersReceiptDraft()
    {
        await using var factory = new Factory();
        await factory.Seed();
        using var first = await Login(factory, "alice");
        using var second = await Login(factory, "alice");
        const string path = "/api/preferences/receipt-draft:test-concurrent-version";
        UniqueKey(first);
        var firstWrite = await first.PutAsJsonAsync(path,
            new { draftRevision = 0, data = new { amount = "12.34", supplier = "First tab" },
                taxes = new { }, sourceVersion = 5 });
        Assert.Equal(HttpStatusCode.OK, firstWrite.StatusCode);
        Assert.Equal(1, (await firstWrite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("draftRevision").GetInt32());

        UniqueKey(second);
        var stale = await second.PutAsJsonAsync(path,
            new { draftRevision = 0, data = new { amount = "99.99", supplier = "Second tab" },
                taxes = new { }, sourceVersion = 5 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflict = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("draft_conflict", conflict.GetProperty("code").GetString());

        var stored = await first.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(1, stored.GetProperty("draftRevision").GetInt32());
        Assert.Equal("12.34", stored.GetProperty("data").GetProperty("amount").GetString());

        UniqueKey(second);
        var revisionTwo = await second.PutAsJsonAsync(path,
            new { draftRevision = 1, data = new { amount = "77.77", supplier = "Reviewed update" },
                taxes = new { }, sourceVersion = 5 });
        Assert.Equal(HttpStatusCode.OK, revisionTwo.StatusCode);
        Assert.Equal(2, (await revisionTwo.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("draftRevision").GetInt32());

        UniqueKey(first);
        Assert.Equal(HttpStatusCode.Conflict, (await first.PutAsJsonAsync(path,
            new { draftRevision = 1, data = new { amount = "0.01" } })).StatusCode);
        stored = await first.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("77.77", stored.GetProperty("data").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task ReceiptDraftsCannotBeReadByAnotherEmployee()
    {
        await using var factory = new Factory();
        await factory.Seed();
        using var alice = await Login(factory, "alice");
        using var bob = await Login(factory, "bob");
        UniqueKey(alice);
        var saved = await alice.PutAsJsonAsync("/api/preferences/receipt-draft:private-supplier",
            new { draftRevision = 0, data = new { supplier = "Private merchant" }, taxes = new { } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("{}", await bob.GetStringAsync("/api/preferences/receipt-draft:private-supplier"));
    }
}
