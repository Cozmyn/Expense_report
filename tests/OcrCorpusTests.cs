using System.Text.Json;
using Decont;
using Xunit;

namespace Decont.Tests;

public record CorpusExpected(string Invoice, string? Amount, decimal? Vat);

public record CorpusCase(
    string Id,
    string[] Tags,
    List<OcrPage> Pages,
    List<CorpusExpected> Expected,
    int[] Unassigned
);

public class OcrCorpusTests
{
    [Fact]
    public void TaggedCorpusProducesSeparateBoundaryFieldAndCoverageMeasurements()
    {
        var corpus = Serialization.Read<List<CorpusCase>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ocr-corpus.json"))
        );
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int expectedGroups = 0,
            matchedGroups = 0,
            pages = 0,
            represented = 0,
            unassigned = 0;
        Dictionary<string, int[]> metrics = new()
        {
            { "invoice", [0, 0, 0] },
            { "gross", [0, 0, 0] },
            { "vat", [0, 0, 0] },
        };
        List<object> outcomes = [];
        foreach (var fixture in corpus)
        {
            var result = ReceiptParser.Parse(fixture.Pages, null);
            Assert.Equal(fixture.Expected.Count, result.Candidates.Count);
            Assert.Equal(fixture.Unassigned, result.UnassignedPages);
            expectedGroups += fixture.Expected.Count;
            pages += fixture.Pages.Count;
            represented += result.Pages.Select(p => p.Number).Distinct().Count();
            unassigned += result.UnassignedPages!.Length;
            foreach (var expected in fixture.Expected)
            {
                var candidate = Assert.Single(
                    result.Candidates,
                    c => c.Fields.GetValueOrDefault("invoice")?.Value == expected.Invoice
                );
                matchedGroups++;
                void Field(string name, string? actual, string? gold)
                {
                    var m = metrics[name];
                    if (actual != null)
                        m[1]++;
                    if (gold != null)
                        m[2]++;
                    if (actual != null && actual == gold)
                        m[0]++;
                    Assert.Equal(gold, actual);
                }
                Field("invoice", candidate.Fields.GetValueOrDefault("invoice")?.Value, expected.Invoice);
                Field("gross", candidate.Fields.GetValueOrDefault("amount")?.Value, expected.Amount);
                Field(
                    "vat",
                    candidate.Taxes.Count == 0
                        ? null
                        : candidate
                            .Taxes.Sum(t => t.Vat)
                            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    expected.Vat?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                );
            }
            outcomes.Add(
                new
                {
                    fixture.Id,
                    fixture.Tags,
                    groups = result.Candidates.Count,
                    pages = result.Pages.Count,
                    unassigned = result.UnassignedPages,
                }
            );
        }
        var report = new
        {
            corpus = "synthetic-parser-v1",
            businessApproved = false,
            scope = "Deterministic parser regression; not real-image OCR accuracy",
            cases = corpus.Count,
            expectedGroups,
            matchedGroups,
            segmentationRecall = expectedGroups == 0 ? 0m : (decimal)matchedGroups / expectedGroups,
            pageRepresentation = (decimal)represented / pages,
            unassignedPageRate = (decimal)unassigned / pages,
            fields = metrics.ToDictionary(
                p => p.Key,
                p => new
                {
                    correct = p.Value[0],
                    predicted = p.Value[1],
                    expected = p.Value[2],
                    precision = p.Value[1] == 0 ? (decimal?)null : (decimal)p.Value[0] / p.Value[1],
                    recall = p.Value[2] == 0 ? (decimal?)null : (decimal)p.Value[0] / p.Value[2],
                }
            ),
            elapsedMilliseconds = watch.ElapsedMilliseconds,
            outcomes,
        };
        var destination = Environment.GetEnvironmentVariable("OCR_QUALITY_OUTPUT");
        if (!string.IsNullOrWhiteSpace(destination))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            File.WriteAllText(
                destination,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions(Serialization.Options) { WriteIndented = true }
                )
            );
        }
        Assert.Equal(pages, represented);
    }
}
