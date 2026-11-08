using Decont;
using Xunit;

namespace Decont.Tests;

public class DocumentOrientationTests
{
    [Fact]
    public void RotatedWordBoxesMapBackToTheUnchangedOriginal()
    {
        var b = new SourceBox(.1m, .2m, .3m, .4m);
        Assert.Equal(new SourceBox(.2m, .6m, .4m, .3m), DocumentOrientation.ToOriginal(b, 90));
        Assert.Equal(new SourceBox(.6m, .4m, .3m, .4m), DocumentOrientation.ToOriginal(b, 180));
        Assert.Equal(new SourceBox(.4m, .1m, .4m, .3m), DocumentOrientation.ToOriginal(b, 270));
        Assert.Equal(b, DocumentOrientation.ToOriginal(b, 0));
    }
}
