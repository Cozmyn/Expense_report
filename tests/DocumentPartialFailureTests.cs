using Decont;
using Xunit;
using Microsoft.Extensions.Configuration;
namespace Decont.Tests;

public class DocumentPartialFailureTests
{
    [Fact]
    public async Task AFailedRenderOnOnePageDoesNotDiscardAnotherPagesTextLayer()
    {
        if (OperatingSystem.IsWindows()) return; var root = Path.Combine(Path.GetTempPath(), "decont-provider-test-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string Script(string name, string code) { var path = Path.Combine(root, name); File.WriteAllText(path, "#!/bin/sh\n" + code); File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); return path; }
            var info = Script("pdfinfo", "printf 'Pages: 2\\nPage 1 rot: 0\\nPage 2 rot: 0\\n'\n");
            var text = Script("pdftotext", "if [ \"$2\" = \"1\" ]; then\nprintf '%s' '<doc><page width=\"500\" height=\"800\"><line><word xMin=\"10\" yMin=\"10\" xMax=\"100\" yMax=\"30\">INVOICE</word><word xMin=\"110\" yMin=\"10\" xMax=\"130\" yMax=\"30\">NO</word><word xMin=\"140\" yMin=\"10\" xMax=\"180\" yMax=\"30\">GOOD</word></line><line><word xMin=\"10\" yMin=\"50\" xMax=\"100\" yMax=\"70\">TOTAL</word><word xMin=\"110\" yMin=\"50\" xMax=\"160\" yMax=\"70\">10.00</word><word xMin=\"170\" yMin=\"50\" xMax=\"210\" yMax=\"70\">RON</word></line></page></doc>'\nelse\nexit 1\nfi\n");
            var render = Script("pdftoppm", "exit 1\n"); var image = Script("tesseract", "printf 'eng\\nron\\nosd\\n'\n"); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Ocr:PdfInfo", info }, { "Ocr:PdfText", text }, { "Ocr:PdfRenderer", render }, { "Ocr:Tesseract", image } }).Build(); List<OcrProgress> progress = []; var result = await new LocalDocumentExtractor(config).ExtractProgress("%PDF-synthetic"u8.ToArray(), "fixture.pdf", "application/pdf", null, p => { progress.Add(p); return Task.CompletedTask; }, CancellationToken.None); Assert.Equal("PartiallyProcessed", result.State); Assert.Equal(2, result.Pages.Count); Assert.Equal("ExtractionFailed", result.Pages[1].Error); Assert.Equal("GOOD", Assert.Single(result.Candidates).Fields["invoice"].Value); Assert.Equal(new[] { 2 }, result.UnassignedPages); Assert.Contains(progress, p => p.CompletedPages == 2 && p.FailedPages == 1);
        }
        finally { Directory.Delete(root, true); }
    }
}
