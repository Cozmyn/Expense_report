using Decont;
using Xunit;

namespace Decont.Tests;

public class PaddleOcrAdapterTests
{
    [Fact]
    public void ConvertsRealPaddleResponseShapeToNormalizedDocumentWords()
    {
        const string payload = """
            {
              "engine":"PP-OCRv6","width":1000,"height":2000,
              "words":[
                {"text":"BON FISCAL","x":0.12,"y":0.05,"width":0.38,"height":0.02,"confidence":0.96},
                {"text":"TOTAL 123,45 RON","x":0.1,"y":0.82,"width":0.5,"height":0.03,"confidence":0.89}
              ]
            }
            """;
        var page = PaddleOcrClient.ConvertResult(2, payload);

        Assert.Equal(2, page.Number);
        Assert.Equal("ImageOcr", page.Method);
        Assert.Equal(1000m, page.Width);
        Assert.Equal(2000m, page.Height);
        Assert.Equal(5, page.Words!.Count);
        var total = Assert.Single(page.Words, w => w.Text == "TOTAL");
        Assert.Equal(.1m, total.Box.X);
        Assert.Equal(.82m, total.Box.Y);
        Assert.Equal(.89m, total.Confidence);
        Assert.True(total.Box.Width > 0 && total.Box.Width < .5m); // Approximate token box within the OCR line.
        Assert.Contains("BON FISCAL", page.Text);
        Assert.Contains("TOTAL 123,45 RON", page.Text);
    }

    [Fact]
    public void AScannedMultiReceiptPageStillProducesIndependentCandidates()
    {
        const string payload = """
            {
              "width":800,"height":1000,"words":[
                {"text":"BON FISCAL","x":0.05,"y":0.05,"width":0.25,"height":0.03,"confidence":0.99},
                {"text":"BON NR 1","x":0.05,"y":0.09,"width":0.25,"height":0.03,"confidence":0.9},
                {"text":"TOTAL 12,00 RON","x":0.05,"y":0.30,"width":0.4,"height":0.03,"confidence":0.9},
                {"text":"BON FISCAL","x":0.05,"y":0.51,"width":0.25,"height":0.03,"confidence":0.9},
                {"text":"BON NR 2","x":0.05,"y":0.55,"width":0.25,"height":0.03,"confidence":0.9},
                {"text":"TOTAL 25,00 RON","x":0.05,"y":0.84,"width":0.4,"height":0.03,"confidence":0.9}
              ]
            }
            """;
        var source = PaddleOcrClient.ConvertResult(1, payload);
        var extracted = ReceiptParser.Parse([source], null);

        Assert.Equal(2, extracted.Candidates.Count);
        Assert.Equal(new[] { "1", "2" },
            extracted.Candidates.Select(c => c.Fields["invoice"].Value).Order().ToArray());
    }

    [Fact]
    public void InvalidGeometryFailsInsteadOfSilentlyAssigningMoney()
    {
        const string payload = """{"width":0,"height":100,"words":[]}""";
        Assert.ThrowsAny<Exception>(() => PaddleOcrClient.ConvertResult(1, payload));
    }
}
