using Decont;
using Xunit;

namespace Decont.Tests;

public class OcrRegionRecognitionTests
{
    [Fact]
    public void CroppingKeepsOnlyTheSelectedBounds()
    {
        var box = OcrRegionRecognition.Crop(new SourceRegion(2, .25m, .50m, .25m, .20m), 2400, 1600);
        Assert.Equal((600, 800, 600, 320), box);
    }

    [Fact]
    public void TinyOrOversizedRegionsCannotTriggerUnboundedReRecognition()
    {
        Assert.Throws<DomainException>(() =>
            OcrRegionRecognition.Crop(new SourceRegion(1, 0m, 0m, .001m, .001m), 2000, 2000));
        Assert.Throws<DomainException>(() =>
            OcrRegionRecognition.Crop(new SourceRegion(1, 0m, 0m, 1m, 1m), 5000, 5000));
    }
}
