using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Decont;

/// <summary>
/// Single active image-recognition engine. PDF text layers remain lossless native text extraction.
/// The service is private to the deployment network; it never receives other user metadata.
/// </summary>
public static class PaddleOcrClient
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };

    private static Uri Endpoint(IConfiguration config, string path)
    {
        var root = config["Ocr:ServiceUrl"] ?? "http://127.0.0.1:8750";
        Guard.Require(
            Uri.TryCreate(root, UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                && parsed.UserInfo.Length == 0
                && parsed.Query.Length == 0
                && parsed.Fragment.Length == 0,
            "ocr_service",
            "Configurează serviciul intern PaddleOCR.",
            "Configure the internal PaddleOCR service."
        );
        return new Uri(parsed!.ToString().TrimEnd('/') + path);
    }

    public static async Task<(string? Engine, string[] Languages)> Health(IConfiguration config, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await Client.GetAsync(Endpoint(config, "/health"), timeout.Token);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = json.RootElement;
            if (root.GetProperty("status").GetString() != "Ready")
                return (null, []);
            return (
                root.GetProperty("engine").GetString(),
                root.GetProperty("languages").EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            );
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return (null, []);
        }
    }

    public static OcrPage ConvertResult(int number, string json)
    {
        using var data = JsonDocument.Parse(json);
        var root = data.RootElement;
        var width = root.GetProperty("width").GetDecimal();
        var height = root.GetProperty("height").GetDecimal();
        Guard.Require(
            width > 0 && height > 0,
            "ocr_geometry",
            "Dimensiuni OCR invalide.",
            "Invalid OCR dimensions."
        );
        var words = root.GetProperty("words")
            .EnumerateArray()
            .Take(5000)
            .Select(row => new OcrWord(
                row.GetProperty("text").GetString() ?? "",
                new SourceBox(
                    row.GetProperty("x").GetDecimal(),
                    row.GetProperty("y").GetDecimal(),
                    row.GetProperty("width").GetDecimal(),
                    row.GetProperty("height").GetDecimal()
                ),
                row.GetProperty("confidence").GetDecimal()
            ))
            .Where(word =>
                word.Box.X >= 0 && word.Box.Y >= 0 && word.Box.Width > 0 && word.Box.Height > 0
                && word.Box.X + word.Box.Width <= 1 && word.Box.Y + word.Box.Height <= 1
            )
            .ToList();
        var text = string.Join("\n", words.Select(w => w.Text));
        Guard.Require(
            text.Length <= 200000,
            "ocr_text_limit",
            "Pagina are prea mult text. Verifică manual.",
            "Page text exceeds processing limits. Review manually."
        );
        return new OcrPage(number, text, Words: words, Method: "ImageOcr", Width: width, Height: height);
    }

    public static async Task<OcrPage> Recognize(
        IConfiguration config,
        string imagePath,
        int page,
        CancellationToken ct
    )
    {
        var bytes = await File.ReadAllBytesAsync(imagePath, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(config, "/ocr"))
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await Client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        return ConvertResult(page, result);
    }
}
