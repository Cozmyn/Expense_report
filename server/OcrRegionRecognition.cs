using System.Globalization;
using System.Security.Cryptography;

namespace Decont;

/// <summary>
/// Re-recognise only an explicitly selected source crop with the existing private
/// PaddleOCR engine. This does not modify the original, verified accounting data,
/// or historical extraction attempts.
/// </summary>
public static class OcrRegionRecognition
{
    public static (int X, int Y, int Width, int Height) Crop(SourceRegion region, int width, int height)
    {
        Guard.Require(width is > 0 and <= 2400 && height is > 0 and <= 2400,
            "ocr_crop_size", "Dimensiuni invalide pentru OCR.", "Invalid OCR crop dimensions.");
        var left = Math.Clamp((int)Math.Floor(region.X * width), 0, width - 1);
        var top = Math.Clamp((int)Math.Floor(region.Y * height), 0, height - 1);
        var right = Math.Clamp((int)Math.Ceiling((region.X + region.Width) * width), left + 1, width);
        var bottom = Math.Clamp((int)Math.Ceiling((region.Y + region.Height) * height), top + 1, height);
        Guard.Require(right - left >= 12 && bottom - top >= 12,
            "ocr_crop_size", "Selectează o zonă mai mare pentru recitire.", "Select a larger region to re-read.");
        return (left, top, right - left, bottom - top);
    }

    public static async Task<string> Recognize(
        IOriginalDocument original, IStore store, IConfiguration config,
        SourceRegion region, CancellationToken cancellationToken)
    {
        Guard.Require(original.Mime is "application/pdf" or "image/png" or "image/jpeg" or "image/heic",
            "ocr_crop_type", "Acest tip de fișier nu poate fi recitit.", "This file type cannot be re-read.");
        var bytes = await store.Get(original.Key, cancellationToken);
        Guard.Require(
            Convert.ToHexString(SHA256.HashData(bytes)) == original.Hash,
            "integrity", "Integritatea originalului nu este validă.", "Original file integrity check failed.");
        var temp = Path.Combine(Path.GetTempPath(), "decont-crop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var extension = original.Mime switch
            {
                "application/pdf" => ".pdf",
                "image/jpeg" => ".jpg",
                "image/heic" => ".heic",
                _ => ".png",
            };
            var input = Path.Combine(temp, "original" + extension);
            await File.WriteAllBytesAsync(input, bytes, cancellationToken);
            var page = Path.Combine(temp, "page.png");
            if (original.Mime == "application/pdf")
            {
                await LocalDocumentExtractor.Run(
                    config["Ocr:PdfRenderer"] ?? "pdftoppm",
                    ["-f", region.Page.ToString(CultureInfo.InvariantCulture),
                     "-l", region.Page.ToString(CultureInfo.InvariantCulture),
                     "-singlefile", "-scale-to", "2400", "-png",
                     input, Path.Combine(temp, "page")],
                    cancellationToken);
            }
            else if (original.Mime == "image/heic")
            {
                await LocalDocumentExtractor.Run(
                    config["Ocr:HeicConverter"] ?? "heif-convert",
                    [input, page], cancellationToken);
            }
            else
                page = input;

            var convert = config["Ocr:ImageRotator"] ?? "convert";
            var normalized = Path.Combine(temp, "normalized.png");
            string[] limits = ["-limit", "memory", "128MiB",
                "-limit", "map", "128MiB", "-limit", "disk", "0",
                "-limit", "thread", "1"];
            await LocalDocumentExtractor.Run(convert,
                [..limits, page, "-resize", "2400x2400>", "png:" + normalized],
                cancellationToken);
            var dimensionText = await LocalDocumentExtractor.Run(convert,
                [..limits, normalized, "-format", "%w %h", "info:"], cancellationToken);
            var dimensions = dimensionText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var width = 0;
            var height = 0;
            Guard.Require(dimensions.Length == 2
                && int.TryParse(dimensions[0], out width)
                && int.TryParse(dimensions[1], out height),
                "ocr_crop_size", "Imagine imposibil de măsurat.", "Could not measure image.");
            var box = Crop(region, width, height);
            var cropped = Path.Combine(temp, "selection.png");
            await LocalDocumentExtractor.Run(convert,
                [..limits, normalized, "-crop",
                 box.Width + "x" + box.Height + "+" + box.X + "+" + box.Y,
                 "+repage", "-resize", "1800x1800>", "png:" + cropped],
                cancellationToken);
            var recognised = await PaddleOcrClient.Recognize(config, cropped, region.Page, cancellationToken);
            Guard.Require(recognised.Text.Length <= 1000, "ocr_crop_text",
                "Zona conține prea mult text; selectează o regiune mai mică.",
                "Region contains too much text; select a smaller area.");
            return recognised.Text;
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }
}
