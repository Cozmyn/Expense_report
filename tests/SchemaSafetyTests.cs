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
        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.VerifyModel(db);
    }

    [Fact]
    public async Task MissingIndexFailsClosedInsteadOfQuietlyAcceptingSchemaDrift()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await SchemaUpgrades.Apply(db);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");
        else
            await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SchemaUpgrades.VerifyModel(db));
        Assert.Contains("IX_DocumentGroups_BundleId_SourceKey", exception.Message);
    }

    [Fact]
    public async Task VerifiedBaselineNeverSilentlyRecreatesMissingIndex()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await SchemaUpgrades.Apply(db);
        Assert.True(await db.SchemaReleases.AnyAsync(r => r.Id == 13));
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SchemaUpgrades.Apply(db));
        Assert.Contains("IX_DocumentGroups_BundleId_SourceKey", failure.Message);
        // This matters: the initializer must not modify or 'repair' an
        // already-baselined database outside reviewed versioned migrations.
    }


    [Fact]
    public async Task VerifiedBaselineRejectsMissingImmutableEvidenceTrigger()
    {
        await using var f = new Factory();
        await f.Seed();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await SchemaUpgrades.Apply(db);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER immutable_evidence ON \"DocumentEvents\"");
        else
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER immutable_update_DocumentEvents");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SchemaUpgrades.Apply(db));
        Assert.Contains("immutable_", failure.Message);
    }

}
