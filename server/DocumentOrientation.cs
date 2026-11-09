namespace Decont;

public record OrientationEvidence(int Degrees, string Text, string Method);

public static class DocumentOrientation
{
    public static SourceBox ToOriginal(SourceBox b, int degrees) =>
        degrees switch
        {
            90 => new(b.Y, 1 - b.X - b.Width, b.Height, b.Width),
            180 => new(1 - b.X - b.Width, 1 - b.Y - b.Height, b.Width, b.Height),
            270 => new(1 - b.Y - b.Height, b.X, b.Height, b.Width),
            _ => b,
        };

    static int Rank(OcrPage page)
    {
        var result = ReceiptParser.Parse([page], null);
        return result.Candidates.Sum(c =>
            (c.Fields.ContainsKey("invoice") ? 5 : 0)
            + (c.Fields.ContainsKey("amount") ? 10 : 0)
            + (c.Fields.ContainsKey("date") ? 2 : 0)
            + (c.Fields.ContainsKey("currency") ? 1 : 0)
        );
    }

    public static async Task<OcrPage> Recover(
        OcrPage baseline,
        string image,
        IConfiguration config,
        CancellationToken ct
    )
    {
        var rank = Rank(baseline);
        if (rank >= 15)
            return baseline;
        var best = baseline;
        List<OrientationEvidence> evidence = [new(0, baseline.Text, "PaddleOCR")];
        foreach (var degrees in new[] { 90, 270, 180 })
        {
            var output = Path.Combine(Path.GetDirectoryName(image)!, "orientation-" + Guid.NewGuid() + ".png");
            try
            {
                var format = Path.GetExtension(image).ToLowerInvariant() is ".jpg" or ".jpeg" ? "jpeg:" : "png:";
                await LocalDocumentExtractor.Run(
                    config["Ocr:ImageRotator"] ?? "convert",
                    [
                        "-limit",
                        "memory",
                        "128MiB",
                        "-limit",
                        "map",
                        "128MiB",
                        "-limit",
                        "disk",
                        "0",
                        "-limit",
                        "thread",
                        "1",
                        format + image,
                        "-resize",
                        "2400x2400>",
                        "-rotate",
                        degrees.ToString(),
                        "png:" + output,
                    ],
                    ct
                );
                var page = await PaddleOcrClient.Recognize(config, output, baseline.Number, ct);
                page = page with
                {
                    Words = page.Words?.Select(w => w with { Box = ToOriginal(w.Box, degrees) }).ToList(),
                    Width = baseline.Width,
                    Height = baseline.Height,
                    RecognitionRotation = degrees,
                };
                evidence.Add(new(degrees, page.Text, "PaddleOCRRotated"));
                var next = Rank(page);
                if (next > rank)
                {
                    best = page;
                    rank = next;
                }
                if (rank >= 15)
                    break;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                evidence.Add(new(degrees, "", "OrientationFailed"));
            }
            finally
            {
                if (File.Exists(output))
                    File.Delete(output);
            }
        }
        return best with { OrientationEvidence = evidence };
    }
}
