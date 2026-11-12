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

    [Fact]
    public void CorrectedSelectionIsNeverAPositiveSupplierLearningExample()
    {
        var expense = new ExpenseData("Vendor", "18546294", "INV-9", "2026-11-11",
            "RO", "InvoiceWithCui", "GENERAL", "Valid receipt", "Personal",
            null, "CC", null, 121m, "RON", []);
        Assert.False(SupplierOcrLearning.MatchesVerifiedValue(expense, "amount",
            new OcrField("100.00", 1, "SUBTOTAL 100.00", Source: "HumanSelected")));
        Assert.True(SupplierOcrLearning.MatchesVerifiedValue(expense, "amount",
            new OcrField("121.00", 1, "TOTAL 121.00", Source: "HumanSelected")));
        Assert.False(SupplierOcrLearning.MatchesVerifiedValue(expense, "supplier",
            new OcrField("Wrong Vendor", 1, "Wrong Vendor", Source: "HumanSelected")));
    }

    [Fact]
    public void SupplierHintsCannotCrossBetweenReceiptsOnTheSamePage()
    {
        var sample = new SourceBox(.10m, .72m, .5m, .04m);
        var examples = new[]
        {
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", sample),
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", sample),
        };
        var receipt = Receipt();
        var first = receipt.Candidates[0] with { Regions = [new SourceRegion(1, .0m, .0m, .6m, 1m)] };
        var second = receipt.Candidates[0] with
        {
            Id = Guid.NewGuid(),
            Regions = [new SourceRegion(1, .6m, .0m, .4m, 1m)]
        };
        var result = SupplierOcrLearning.Suggest(
            receipt with { Candidates = [first, second] }, examples);
        Assert.Equal("123.45", result.Candidates[0].Fields["amount"].Value);
        Assert.False(result.Candidates[1].Fields.ContainsKey("amount"));
    }


    [Fact]
    public void AmbiguousOcrFieldMustNotBeOverwrittenByHistoricalSupplierTemplate()
    {
        var region = new SourceBox(.10m, .72m, .5m, .04m);
        var examples = new[]
        {
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region),
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region)
        };
        var original = Receipt();
        var ambiguous = original.Candidates[0] with
        {
            Fields = new Dictionary<string, OcrField>(original.Candidates[0].Fields)
            {
                ["amount"] = new(null, 1, "TOTAL 100.00 / 500.00", "Ambiguous")
            }
        };
        var suggested = SupplierOcrLearning.Suggest(
            original with { Candidates = [ambiguous] }, examples);
        var value = suggested.Candidates[0].Fields["amount"];
        Assert.Null(value.Value);
        Assert.Equal("Ambiguous", value.State);
        Assert.DoesNotContain(suggested.Candidates[0].Warnings,
            warning => warning.StartsWith("SUPPLIER_HINT"));
    }

    [Fact]
    public void SourceSelectionNeverGuessesWrongGrossOrTaxColumn()
    {
        Assert.Null(OcrFieldSelection.Extract("amount", "TOTAL 100.00 EUR / 500.00 RON"));
        Assert.Equal("100.00", OcrFieldSelection.Extract("amount",
            "TOTAL 100.00 EUR ECHIVALENT 500.00 RON"));
        Assert.Equal("100.00", OcrFieldSelection.Extract("taxBase:21",
            "TVA 21% 100.00 21.00"));
        Assert.Equal("21.00", OcrFieldSelection.Extract("taxVat:21",
            "TVA 21% 100.00 21.00"));
        Assert.Null(OcrFieldSelection.Extract("taxBase:21",
            "TVA 21% 100.00 21.00 121.00"));
        Assert.Null(OcrFieldSelection.Extract("taxVat:11",
            "TVA 21% 100.00 21.00"));
    }

    [Fact]
    public void MultiDateMultiCurrencyAndMultiCuiSelectionRequiresManualCorrection()
    {
        Assert.Null(OcrFieldSelection.Extract("date", "10.11.2026 12.11.2026"));
        Assert.Null(OcrFieldSelection.Extract("date", "02/03/2026"));
        Assert.Null(OcrFieldSelection.Extract("currency", "100.00 EUR / 500.00 RON"));
        Assert.Equal("EUR", OcrFieldSelection.Extract("currency",
            "TOTAL 100.00 EUR ECHIVALENT 500.00 RON"));
        Assert.Null(OcrFieldSelection.Extract("cui", "CUI 18546294 CUI 35898037"));
    }

    [Fact]
    public void LearnedHintHighlightsTheCurrentDocumentTextNotTheHistoricalBox()
    {
        var region = new SourceBox(.08m, .69m, .55m, .07m);
        var examples = new[]
        {
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region),
            new SupplierOcrLearning.Example("18546294", Guid.NewGuid(), "amount", region)
        };
        var hint = SupplierOcrLearning.Suggest(Receipt(), examples).Candidates[0].Fields["amount"];
        Assert.Equal(new SourceBox(.10m, .72m, .5m, .04m), hint.Box);
        Assert.Equal("SupplierTemplate", hint.Source);
        Assert.Equal("ReviewRequired", hint.State);
    }


}
