using Decont;
using Xunit;

namespace Decont.Tests;

public class JobRecoveryTests
{
    [Fact]
    public void CrashedOcrOrOutboxJobsCanBeClaimedByAnotherInstanceAfterLeaseExpires()
    {
        var now = DateTime.UtcNow;
        var predicate = JobRecovery.Claimable(now).Compile();
        foreach (var kind in new[] { "Scan", "ScanReport", "Ocr", "OcrReport", "DocumentEvent" })
        {
            Assert.True(predicate(new Job { Kind = kind, State = "Processing", Attempts = 1,
                Due = now.AddSeconds(-1) }));
            Assert.False(predicate(new Job { Kind = kind, State = "Processing", Attempts = 1,
                Due = now.AddMinutes(3) }));
            Assert.True(predicate(new Job { Kind = kind, State = "Pending", Attempts = 2,
                Due = now.AddSeconds(-1) }));
        }
    }

    [Fact]
    public void EmailNeverAutomaticallyReplaysAfterPotentialSmtpDelivery()
    {
        var now = DateTime.UtcNow;
        var predicate = JobRecovery.Claimable(now).Compile();
        Assert.True(predicate(new Job { Kind = "Email", State = "Pending", Attempts = 0,
            Due = now.AddSeconds(-1) }));
        Assert.False(predicate(new Job { Kind = "Email", State = "Processing", Attempts = 1,
            Due = now.AddMinutes(-20) }));
        Assert.False(predicate(new Job { Kind = "Email", State = "Pending", Attempts = 1,
            Due = now.AddMinutes(-20) }));
        Assert.False(predicate(new Job { Kind = "Email", State = "Failed", Attempts = 1,
            Due = now.AddMinutes(-20) }));
    }
}
