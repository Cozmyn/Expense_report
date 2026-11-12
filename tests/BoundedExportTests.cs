using Decont;
using Xunit;

namespace Decont.Tests;

public class BoundedExportTests
{
    [Fact]
    public async Task ConcurrentPdfGenerationUsesAtMostTwoSlots()
    {
        int active = 0, peak = 0;
        using var gate = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        async Task<byte[]> Generate()
        {
            var running = Interlocked.Increment(ref active);
            var old = Volatile.Read(ref peak);
            while (running > old)
            {
                var prior = Interlocked.CompareExchange(ref peak, running, old);
                if (prior == old) break;
                old = prior;
            }
            try
            {
                await Task.Delay(80, gate.Token);
                return [37, 80, 68, 70]; // synthetic, no real PDF renderer necessary
            }
            finally { Interlocked.Decrement(ref active); }
        }
        var outputs = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Documents.LimitedPdf(Generate, gate.Token)));
        Assert.Equal(8, outputs.Length);
        Assert.All(outputs, x => Assert.Equal(4, x.Length));
        Assert.InRange(peak, 1, 2);
    }

    [Fact]
    public async Task ExcessivePdfOutputFailsBeforeDownload()
    {
        var oversized = new byte[Documents.MaximumPdfBytes + 1];
        using var token = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            Documents.LimitedPdf(() => Task.FromResult(oversized), token.Token));
        Assert.Equal("pdf_limit", error.Code);
        Assert.Equal(413, error.Status);
        // A rejected request releases its semaphore for subsequent exports.
        Assert.Equal(1, (await Documents.LimitedPdf(
            () => Task.FromResult(new byte[] { 1 }), token.Token)).Length);
    }
}
