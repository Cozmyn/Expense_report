using Decont;
using Xunit;

namespace Decont.Tests;

public class ReceiptCoverageTests
{
    private static DocumentGroup Group(int page, decimal x, string state = "Verified") => new()
    {
        BundleId = Guid.NewGuid(),
        State = state,
        Regions = Serialization.Write(new [] { new SourceRegion(page, x, 0, .4m, 1) })
    };

    [Fact]
    public void PageWithMultipleReceiptsRequiresHumanCountEvenThoughAssigned()
    {
        var review = new DocumentReview { Id = Guid.NewGuid(), PageCount = 1 };
        var first = Group(1, 0);
        var second = Group(1, .5m);
        Assert.Equal(2, OcrReviews.VisibleGroupsOnPage([first, second], 1));
        Assert.Equal([1], OcrReviews.MissingReceiptCoverage(review, [first, second], []));
    }

    [Fact]
    public void ConfirmationIsInvalidatedWhenAReceiptIsAddedOrItsBoundaryChanges()
    {
        var review = new DocumentReview { Id = Guid.NewGuid(), PageCount = 2 };
        var first = Group(1, 0);
        var second = Group(2, 0);
        var groups = new[] { first, second };
        var signature = OcrReviews.CoverageSignature(review, groups, 1);
        var verified = new DocumentEvent
        {
            DocumentId = review.Id,
            Type = "ReceiptCoverageConfirmed",
            Payload = Serialization.Write(new OcrReviews.PageCoverageEvidence(1, 1, signature))
        };
        Assert.Equal([2], OcrReviews.MissingReceiptCoverage(review, groups, [verified]));
        // OCR identified only one receipt; the employee draws another region.
        var missed = Group(1, .5m);
        Assert.Equal([1, 2], OcrReviews.MissingReceiptCoverage(review, [first, missed, second], [verified]));
        first.Regions = Serialization.Write(new[] { new SourceRegion(1, 0, 0, .3m, 1) });
        Assert.Contains(1, OcrReviews.MissingReceiptCoverage(review, groups, [verified]));
    }

    [Fact]
    public void AChangedReceiptStatusInvalidatesAnEarlierConfirmation()
    {
        var review = new DocumentReview { PageCount = 1 };
        var group = Group(1, 0);
        var signature = OcrReviews.CoverageSignature(review, [group], 1);
        var confirmation = new DocumentEvent
        {
            Type = "ReceiptCoverageConfirmed",
            Payload = Serialization.Write(new OcrReviews.PageCoverageEvidence(1, 1, signature))
        };
        group.State = "ReviewRequired";
        Assert.Equal([1], OcrReviews.MissingReceiptCoverage(review, [group], [confirmation]));
    }

    [Fact]
    public void SupportingPagesStillRequireExplicitVisualConfirmation()
    {
        var review = new DocumentReview {
            PageCount = 1,
            Dispositions = Serialization.Write(new[] { new PageDisposition(1, "Supporting attachment; no receipt") })
        };
        Assert.Equal([1], OcrReviews.MissingReceiptCoverage(review, [], []));
        var signature = OcrReviews.CoverageSignature(review, [], 1);
        var confirmation = new DocumentEvent {
            Type = "ReceiptCoverageConfirmed",
            Payload = Serialization.Write(new OcrReviews.PageCoverageEvidence(1, 0, signature))
        };
        Assert.Empty(OcrReviews.MissingReceiptCoverage(review, [], [confirmation]));
    }

    [Fact]
    public void SingleReceiptDoesNotChangeExistingMandatoryFlowUntilItIsSplit()
    {
        var attachment = new ReportAttachment { Capture = true, ReceiptMode = "Single" };
        var group = Group(1, 0);
        Assert.False(OcrReviews.RequiresReceiptCoverage(attachment, [group]));
        var tombstone = Group(1, .5m, "Split");
        Assert.True(OcrReviews.RequiresReceiptCoverage(attachment, [group, tombstone]));
    }
}
