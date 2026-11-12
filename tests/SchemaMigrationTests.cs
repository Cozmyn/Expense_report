using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class SchemaMigrationTests
{
    [Fact]
    public async Task FreshDatabaseUsesPinnedBaselineAndImmutableTriggers()
    {
        await using var factory = new Factory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();

        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.VerifyRuntime(db);
        Assert.True(await db.SchemaReleases.AnyAsync(x => x.Id == 14));
        Assert.True(await db.SchemaReleases.AnyAsync(x => x.Id == 13));
        // Re-running the privileged job is harmless and does not touch data.
        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.VerifyRuntime(db);
    }

    [Fact]
    public async Task LegacyUpgradeRestoresMissingTablesColumnsAndIndexesWithoutChangingUsers()
    {
        await using var factory = new Factory();
        await factory.Seed();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var userBefore = await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName).Select(u => new { u.Id, u.UserName, u.PasswordHash }).ToListAsync();

        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"DocumentReviews\"");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ReportAttachments\" DROP COLUMN \"ReceiptMode\"");
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_DocumentGroups_BundleId_SourceKey\"");

        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.VerifyRuntime(db);
        var userAfter = await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName).Select(u => new { u.Id, u.UserName, u.PasswordHash }).ToListAsync();
        Assert.Equal(userBefore, userAfter);
        Assert.Equal(14, await db.SchemaReleases.MaxAsync(x => x.Id));
        await SchemaUpgrades.Apply(db);
        await SchemaUpgrades.VerifyRuntime(db);
    }

    [Fact]
    public async Task UnexpectedPartialSchemaIsRejectedAndReleaseNotStamped()
    {
        await using var factory = new Factory();
        await factory.Seed();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        // The bridge must never mark an incomplete legacy table as healthy.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"DocumentReviews\"");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE \"DocumentReviews\" (\"Id\" TEXT NOT NULL PRIMARY KEY)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SchemaUpgrades.Apply(db));
        Assert.False(await db.SchemaReleases.AnyAsync(x => x.Id == 13));
        Assert.False(await db.SchemaReleases.AnyAsync(x => x.Id == 14));
    }

    [Fact]
    public async Task ReviewedBaselineMatchesTheEFModelWithoutDynamicProductionDDL()
    {
        await using var factory = new Factory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var provider = db.Database.IsNpgsql() ? "postgres" : "sqlite";
        var resource = typeof(SchemaUpgrades).Assembly.GetManifestResourceStream(
            "decont.migrations.001-baseline." + provider + ".sql");
        Assert.NotNull(resource);
        using var reader = new StreamReader(resource!);
        var baseline = await reader.ReadToEndAsync();
        Assert.Equal(db.Database.GenerateCreateScript().Replace("\r\n", "\n").Trim(),
            baseline.Replace("\r\n", "\n").Trim());
    }
}
