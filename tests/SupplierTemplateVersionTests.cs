using Decont;
using Xunit;

namespace Decont.Tests;

public class SupplierTemplateVersionTests
{
    const string Cui = "18546294";

    static SupplierOcrLearning.Example Example(
        Guid bundle, decimal y, string layout = "branch-a") =>
        new(Cui, bundle, "amount", new SourceBox(.1m, y, .5m, .04m), layout);

    [Fact]
    public void NewRevisionDisablesOneVariantWithoutDeletingHistoryOrAnotherVariant()
    {
        var a = new SupplierOcrTemplates.Version(Cui, "branch-a", "Till A", 1, true,
            [Example(Guid.NewGuid(), .72m), Example(Guid.NewGuid(), .72m)]);
        var aDisabled = a with { Revision = 2, Enabled = false };
        var b = new SupplierOcrTemplates.Version(Cui, "branch-b", "Till B", 1, true,
            [Example(Guid.NewGuid(), .18m, "branch-b"),
                Example(Guid.NewGuid(), .18m, "branch-b")]);
        var versions = new[] { a, aDisabled, b };
        Assert.Equal(3, versions.Length); // History remains immutable and accessible.
        var latest = SupplierOcrTemplates.Latest(versions).ToList();
        Assert.Equal(2, latest.Count);
        Assert.False(latest.Single(v => v.LayoutId == "branch-a").Enabled);
        Assert.True(latest.Single(v => v.LayoutId == "branch-b").Enabled);
        Assert.Equal(a.Examples, latest.Single(v => v.LayoutId == "branch-a").Examples);
    }

    [Fact]
    public void TemplateSamplesMustComeFromTwoSeparateVerifiedOriginalsWithMatchingRegions()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var examples = new [] {
            Example(first, .72m), Example(second, .73m),
            new SupplierOcrLearning.Example("12345678", Guid.NewGuid(), "amount",
                new(.1m, .72m, .5m, .04m)),
        };
        var selected = SupplierOcrTemplates.Build(Cui, "till-one", examples, [first, second]);
        Assert.Equal(2, selected.Count);
        Assert.All(selected, x => Assert.Equal("till-one", x.LayoutId));
        Assert.Empty(SupplierOcrTemplates.Build(Cui, "till-one",
            examples, [first, Guid.NewGuid()]));
        Assert.Empty(SupplierOcrTemplates.Build(Cui, "till-one",
            [Example(first, .72m), Example(second, .15m)], [first, second]));
    }

    [Fact]
    public void TwoDifferentManagedLayoutsNeverSilentlySelectConflictingAmounts()
    {
        OcrResult result = new(
            "ImageOcr", "ReviewRequired", "",
            [new OcrPage(1, "", Words: [
                new("TOTAL 100,00 RON", new SourceBox(.1m, .72m, .5m, .04m), .9m),
                new("TOTAL 200,00 RON", new SourceBox(.1m, .18m, .5m, .04m), .9m)
            ])],
            [new OcrCandidate(Guid.NewGuid(), [1],
                new Dictionary<string, OcrField> {["cui"] = new(Cui, 1, Cui)}, [], [])]
        );
        var first = Example(Guid.NewGuid(), .72m);
        var second = Example(Guid.NewGuid(), .18m, "branch-b");
        var variants = new[] {
            first, first with { Bundle = Guid.NewGuid() },
            second, second with { Bundle = Guid.NewGuid() }
        };
        var ambiguous = SupplierOcrLearning.Suggest(result, variants);
        Assert.False(ambiguous.Candidates[0].Fields.ContainsKey("amount"));
        Assert.Contains(ambiguous.Candidates[0].Warnings,
            x => x.StartsWith("SUPPLIER_LAYOUT_AMBIGUOUS:"));

        var onlyActive = SupplierOcrLearning.Suggest(result, variants.Take(2));
        Assert.Equal("100.00", onlyActive.Candidates[0].Fields["amount"].Value);
        Assert.Equal("ReviewRequired", onlyActive.Candidates[0].Fields["amount"].State);
    }
}
