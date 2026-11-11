using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class SchemaSafetyTests
{
    [Fact]
    public async Task CurrentDatabaseHasEveryRequiredColumnAndIndex()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await SchemaUpgrades.VerifyModel(db);
    }

    [Fact]
    public async Task MissingIndexFailsClosedInsteadOfQuietlyAcceptingSchemaDrift()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");
        else
            await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SchemaUpgrades.VerifyModel(db));
        Assert.Contains("IX_DocumentGroups_BundleId_SourceKey", exception.Message);
    }
}
