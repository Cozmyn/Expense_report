using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace Decont;

public record SourceBox(decimal X, decimal Y, decimal Width, decimal Height);
public record OcrWord(string Text, SourceBox Box, decimal? Confidence = null);
public static class DocumentGeometry
{
    static decimal Number(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    static string Normalize(string text) => Regex.Replace(text.ToUpperInvariant(), @"[^\p{L}\p{Nd}\p{Sc}]", "");
    public static (SourceBox? Box, decimal? Confidence) Find(OcrPage page, string evidence, int offset = 0, int? count = null)
    {
        if (page.Words == null || evidence.Length > 2000) return (null, null); var words = page.Words.Skip(offset).Take(count ?? int.MaxValue).ToList(); var target = Normalize(evidence); if (target.Length == 0) return (null, null);
        for (int start = 0; start < words.Count; start++) { string joined = ""; for (int end = start; end < Math.Min(words.Count, start + 80); end++) { joined += Normalize(words[end].Text); if (joined == target) { var selected = words.GetRange(start, end - start + 1); var x = selected.Min(w => w.Box.X); var y = selected.Min(w => w.Box.Y); var right = selected.Max(w => w.Box.X + w.Box.Width); var bottom = selected.Max(w => w.Box.Y + w.Box.Height); return (new(x, y, right - x, bottom - y), selected.All(w => w.Confidence != null) ? selected.Min(w => w.Confidence) : null); } if (joined.Length > target.Length || !target.StartsWith(joined, StringComparison.Ordinal)) break; } }
        return (null, null);
    }
    public static OcrPage Tsv(int number, string tsv)
    {
        Guard.Require(tsv.Length <= 2000000, "ocr_layout", "Rezultatul OCR depășește limita.", "OCR layout exceeds limits."); decimal width = 0, height = 0; List<OcrWord> words = []; List<List<OcrWord>> lines = []; string previous = "";
        foreach (var row in tsv.Split('\n'))
        {
            var c = row.TrimEnd('\r').Split('\t', 12, StringSplitOptions.None); if (c.Length < 12 || c[0] == "level") continue; if (c[0] == "1") { width = Number(c[8]); height = Number(c[9]); continue; }
            if (c[0] != "5" || string.IsNullOrWhiteSpace(c[11])) continue;
            Guard.Require(width > 0 && height > 0, "ocr_layout", "Dimensiuni OCR invalide.", "Invalid OCR page dimensions."); var x = Number(c[6]) / width; var y = Number(c[7]) / height; var w = Number(c[8]) / width; var h = Number(c[9]) / height; var confidence = Number(c[10]); var word = new OcrWord(c[11].Trim(), new(x, y, w, h), confidence is >= 0 and <= 100 ? confidence / 100 : null); words.Add(word); var key = c[2] + ":" + c[3] + ":" + c[4]; if (key != previous) { lines.Add([]); previous = key; }
            lines[^1].Add(word);
        }
        return new(number, string.Join("\n", lines.Select(line => string.Join(" ", line.Select(w => w.Text)))), Words: words, Method: "ImageOcr", Width: width, Height: height);
    }
    public static OcrPage PdfBbox(int number, string xml)
    {
        Guard.Require(xml.Length <= 2000000, "ocr_layout", "Textul PDF depășește limita.", "PDF text exceeds limits."); using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }); var doc = XDocument.Load(reader); var page = doc.Descendants().First(e => e.Name.LocalName == "page"); var width = Number(page.Attribute("width")!.Value); var height = Number(page.Attribute("height")!.Value); Guard.Require(width > 0 && height > 0, "ocr_layout", "Dimensiuni PDF invalide.", "Invalid PDF dimensions."); List<OcrWord> words = []; List<string> lines = [];
        foreach (var line in page.Descendants().Where(e => e.Name.LocalName == "line")) { List<string> text = []; foreach (var item in line.Descendants().Where(e => e.Name.LocalName == "word")) { var x = Number(item.Attribute("xMin")!.Value); var y = Number(item.Attribute("yMin")!.Value); var right = Number(item.Attribute("xMax")!.Value); var bottom = Number(item.Attribute("yMax")!.Value); words.Add(new(item.Value, new(x / width, y / height, (right - x) / width, (bottom - y) / height))); text.Add(item.Value); } lines.Add(string.Join(" ", text)); }
        return new(number, string.Join("\n", lines), Words: words, Method: "TextLayer", Width: width, Height: height);
    }
}

