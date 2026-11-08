using Decont;
using Xunit;
namespace Decont.Tests;

public class DocumentSpatialTests
{
    static OcrPage TwoColumns(bool secondInvoice = true) { List<OcrWord> words = []; for (int column = 0; column < 2; column++) { var lines = column == 1 && !secondInvoice ? new[] { "PAYMENT DETAILS", "TOTAL 20.00 RON" } : new[] { "INVOICE NO " + (column == 0 ? "LEFT" : "RIGHT"), "01.01.2026", "TOTAL " + (column == 0 ? "10.00" : "20.00") + " RON" }; for (int line = 0; line < lines.Length; line++) { int index = 0; foreach (var text in lines[line].Split(' ')) words.Add(new(text, new(.05m + column * .52m + index++ * .075m, .1m + line * .15m, .06m, .03m), .95m)); } } return new(1, "INVOICE NO LEFT INVOICE NO RIGHT\nTOTAL 10.00 RON TOTAL 20.00 RON", Words: words, Method: "ImageOcr"); }
    [Fact] public void TwoSideBySideReceiptsHaveIndependentValuesAndNonOverlappingRegions() { var result = ReceiptParser.Parse([TwoColumns()], null); Assert.Equal(2, result.Candidates.Count); Assert.Equal(new[] { "10.00", "20.00" }, result.Candidates.Select(c => c.Fields["amount"].Value)); Assert.False(OcrReviews.Overlap(result.Candidates[0].Regions![0], result.Candidates[1].Regions![0])); Assert.All(result.Candidates, c => Assert.NotNull(c.Fields["amount"].Box)); Assert.Empty(result.UnassignedPages!); Assert.Equal(2, result.Candidates.Select(c => c.Id).Distinct().Count()); }
    [Fact] public void ATaxOrPaymentColumnIsNeverInventedAsASecondReceipt() { Assert.Single(DocumentSegmentation.Panels(TwoColumns(false))); }
}
