using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Decont.Tests;

public class IntervalConcurrencyTests
{
    [Fact]
    public async Task ConcurrentPostgresConfigurationsCannotCommitOverlappingEffectivePeriods()
    {
        await using var fixture = new Factory();
        await fixture.Seed();
        using var scope = fixture.Services.CreateScope();
        if (!scope.ServiceProvider.GetRequiredService<Db>().Database.IsNpgsql())
            return;

        var code = "RACE-" + Guid.NewGuid().ToString("N");
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;

        async Task<bool> TryInsert(DateOnly from)
        {
            using var currentScope = fixture.Services.CreateScope();
            var db = currentScope.ServiceProvider.GetRequiredService<Db>();
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var overlaps = await db.Masters.AnyAsync(m =>
                    m.Kind == "expenseTypes" && m.Code == code && m.Active
                    && m.From <= new DateOnly(2027, 12, 31)
                    && (m.To == null || m.To >= from));
                Assert.False(overlaps);
                if (Interlocked.Increment(ref readers) == 2)
                    ready.TrySetResult(true);
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
                db.Masters.Add(new Master { Kind = "expenseTypes", Code = code,
                    NameRo = "Race test", NameEn = "Race test",
                    From = from, To = new DateOnly(2027, 12, 31) });
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex) when (ex.GetBaseException() is PostgresException pg
                && pg.SqlState is "40001" or "40P01" or "23505")
            {
                return false; // Serializable detected a conflict, including EF wrapper.
            }
        }
        var accepted = await Task.WhenAll(
            TryInsert(new DateOnly(2027, 1, 1)),
            TryInsert(new DateOnly(2027, 6, 1)));
        Assert.Equal(1, accepted.Count(v => v));
        using var verify = fixture.Services.CreateScope();
        var final = verify.ServiceProvider.GetRequiredService<Db>();
        Assert.Single(await final.Masters.Where(m => m.Code == code).ToListAsync());
    }
}
