using Decont;
using Xunit;

namespace Decont.Tests;

public class SupplierOcrLearningTests
{
    private static OcrResult Receipt(string cui = "18546294", string? existingTotal = null)
    {
        var words = new List<OcrWord>
        {
            new("CUI 18546294", new SourceBox(.10m, .15m, .4m, .035m), .93m),
            new("TOTAL 123,45 RON", new SourceBox(.10m, .72m, .5m, .04m), .94m),
        };
        var fields = new Dictionary<string, OcrField>
        {
            ["cui"] = new(cui, 1, "CUI " + cui)
        };
        if (existingTotal != null)
            fields["amount"] = new(existingTotal, 1, "TOTAL " + existingTotal);
        return new(
            "PaddleOCR", "ReviewRequired", "", [new OcrPage(1, "", Words: words)],
            [new OcrCandidate(Guid.NewGuid(), [1], fields, [], [])]
        );
    }

    [Fact]
    public void TwoVerifiedSamplesCanSuggestButNeverConfirmAValue()
    {
        var region = new SourceBox(.10m, .72m, .5m, .04m);
        var examples = new[]
        {
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region),
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region),
        };
        var result = SupplierOcrLearning.Suggest(Receipt(), examples);
        var field = result.Candidates[0].Fields["amount"];

        Assert.Equal("123.45", field.Value);
        Assert.Equal("SupplierTemplate", field.Source);
        Assert.Equal("ReviewRequired", field.State);
        Assert.Null(field.Confidence); // Historical confirmations are not OCR confidence.
        Assert.Contains(result.Candidates[0].Warnings, w => w.StartsWith("SUPPLIER_HINT"));
    }

    [Fact]
    public void NeverLearnFromOneReceiptOrAnotherSupplier()
    {
        var example = new SupplierOcrLearning.Example(
            "18546294", Guid.NewGuid(), "amount", new(.10m, .72m, .5m, .04m)
        );
        Assert.False(SupplierOcrLearning.Suggest(Receipt(), [example]).Candidates[0].Fields.ContainsKey("amount"));
        Assert.False(SupplierOcrLearning.Suggest(
            Receipt("12345678"), [example, example with { Bundle = Guid.NewGuid() }]
        ).Candidates[0].Fields.ContainsKey("amount"));
    }

    [Fact]
    public void NeverOverrideAParsedAmountOrApplyInconsistentLayouts()
    {
        var a = new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", new(.10m, .72m, .5m, .04m));
        var b = a with { Bundle = Guid.NewGuid(), Box = new SourceBox(.85m, .15m, .1m, .03m) };
        Assert.False(SupplierOcrLearning.Suggest(Receipt(), [a, b]).Candidates[0].Fields.ContainsKey("amount"));
        Assert.Equal(
            "900.00",
            SupplierOcrLearning.Suggest(Receipt(existingTotal: "900.00"),
                [a, a with { Bundle = Guid.NewGuid() }]).Candidates[0].Fields["amount"].Value
        );
    }

    [Theory]
    [InlineData("amount", "TOTAL 1.234,56 RON", "1234.56")]
    [InlineData("date", "09.11.2026", "2026-11-09")]
    [InlineData("currency", "RON", "RON")]
    [InlineData("invoice", "BON NR 00019", "00019")]
    public void UserSelectionKeepsTypedValues(string field, string text, string expected) =>
        Assert.Equal(expected, OcrFieldSelection.Extract(field, text));
}
