using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class JobRecoveryIntegrationTests
{
    [Fact]
    public async Task TwoPostgresWorkersCannotClaimSameExpiredLease()
    {
        await using var f = new Factory();
        await f.Seed();
        using var start = f.Services.CreateScope();
        var seed = start.ServiceProvider.GetRequiredService<Db>();
        if (!seed.Database.IsNpgsql()) return;
        var now = DateTime.UtcNow;
        var job = new Job {
            Kind = "OcrReport",
            State = "Processing",
            Due = now.AddMinutes(-20),
            Attempts = 2,
            Json = Guid.NewGuid().ToString()
        };
        seed.Jobs.Add(job);
        await seed.SaveChangesAsync();

        async Task<int> Claim()
        {
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Db>();
            return await db.Jobs.Where(j => j.Id == job.Id)
                .Where(JobRecovery.Claimable(now))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(j => j.State, "Processing")
                    .SetProperty(j => j.Due, now.AddMinutes(5))
                    .SetProperty(j => j.Attempts, j => j.Attempts + 1));
        }
        var outcomes = await Task.WhenAll(Claim(), Claim());
        Assert.Equal(1, outcomes.Sum());
        seed.ChangeTracker.Clear();
        var recovered = await seed.Jobs.SingleAsync(j => j.Id == job.Id);
        Assert.Equal(3, recovered.Attempts);
        Assert.Equal("Processing", recovered.State);
        Assert.True(recovered.Due > now);
    }
}
