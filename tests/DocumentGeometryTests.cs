using Decont;
using Xunit;
namespace Decont.Tests;

public class DocumentGeometryTests
{
    [Fact]
    public void ScanFieldsUseActualWordCoordinatesAndRecognitionScores()
    {
        var tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n1\t1\t0\t0\t0\t0\t0\t0\t1000\t2000\t-1\t\n5\t1\t1\t1\t1\t1\t100\t100\t100\t50\t95\tTOTAL\n5\t1\t1\t1\t1\t2\t250\t100\t100\t50\t85\t121.00\n5\t1\t1\t1\t1\t3\t400\t100\t100\t50\t90\tRON\n";
        var page = DocumentGeometry.Tsv(1, tsv); var field = Assert.Single(ReceiptParser.Parse([page], null).Candidates).Fields["amount"]; Assert.Equal("121.00", field.Value); Assert.Equal(.85m, field.Confidence); Assert.NotNull(field.Box); Assert.Equal(.1m, field.Box!.X); Assert.Equal(.05m, field.Box.Y); Assert.Equal(.4m, field.Box.Width); Assert.Equal("ImageOcr", field.Source); Assert.Contains("121.00", field.RawValue);
    }
    [Fact] public void PdfTextHasCoordinatesWithoutInventedOcrConfidence() { var xml = "<!DOCTYPE doc SYSTEM 'https://example.invalid/not-loaded.dtd'><doc><page width='1000' height='2000'><flow><block><line><word xMin='100' yMin='100' xMax='200' yMax='150'>TOTAL</word><word xMin='250' yMin='100' xMax='350' yMax='150'>121.00</word><word xMin='400' yMin='100' xMax='500' yMax='150'>RON</word></line></block></flow></page></doc>"; var page = DocumentGeometry.PdfBbox(1, xml); var field = Assert.Single(ReceiptParser.Parse([page], null).Candidates).Fields["amount"]; Assert.NotNull(field.Box); Assert.Null(field.Confidence); Assert.Equal("TextLayer", field.Source); }
    [Fact] public void IdenticalTotalsOnOnePageKeepSeparateSourceLocations() { var lines = new[] { "BON FISCAL NR A1", "TOTAL 10.00 RON", "BON FISCAL NR B2", "TOTAL 10.00 RON" }; List<OcrWord> words = []; for (int line = 0; line < lines.Length; line++) { int index = 0; foreach (var word in lines[line].Split(' ')) words.Add(new(word, new(.05m + index++ * .1m, .1m + line * .2m, .08m, .03m), .9m)); } var result = ReceiptParser.Parse([new(1, string.Join("\n", lines), Words: words, Method: "ImageOcr")], null); Assert.Equal(2, result.Candidates.Count); Assert.Equal(.3m, result.Candidates[0].Fields["amount"].Box!.Y); Assert.Equal(.7m, result.Candidates[1].Fields["amount"].Box!.Y); }
    [Fact] public void UnknownCoordinatesAndScoresStayUnknown() { var result = ReceiptParser.Parse([new(1, "INVOICE NO A1\nTOTAL 10.00 RON")], null); Assert.All(Assert.Single(result.Candidates).Fields.Values, f => { Assert.Null(f.Box); Assert.Null(f.Confidence); }); }
}
