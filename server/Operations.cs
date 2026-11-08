using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class Operations
{
    static readonly Meter Meter = new("Decont.Platform", "2.1");
    static readonly Counter<long> Requests = Meter.CreateCounter<long>("decont.http.requests");
    static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("decont.http.duration", "ms");

    public static void Record(int status, double milliseconds)
    {
        Requests.Add(1, new KeyValuePair<string, object?>("status", status));
        Duration.Record(milliseconds);
    }

    public static async Task<object> Dependencies(Db db, IConfiguration config, IDocumentExtractor extractor)
    {
        async Task<string> Reach(string host, int port)
        {
            try
            {
                using var tcp = new TcpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await tcp.ConnectAsync(host, port, timeout.Token);
                return "Reachable";
            }
            catch
            {
                return "Unavailable";
            }
        }
        var scanner = await Reach(config["Scanner:Host"] ?? "localhost", config.GetValue<int?>("Scanner:Port") ?? 3310);
        var smtp =
            !string.IsNullOrWhiteSpace(config["Smtp:Host"]) && config["Smtp:Host"] is string host
                ? await Reach(host, config.GetValue<int?>("Smtp:Port") ?? 587)
                : "NotConfigured";
        var ocr = await extractor.Health(CancellationToken.None);
        return new
        {
            ocrStatus = ocr.Status,
            ocrExtractorVersion = ocr.ParserVersion,
            ocrTextEngine = ocr.TextEngineVersion ?? "Unavailable",
            ocrImageEngine = ocr.ImageEngineVersion ?? "Unavailable",
            ocrMissingLanguages = string.Join(", ", ocr.MissingLanguages ?? []),
            ocrNormalization = ocr.NormalizationStatus,
            ocrImageRotation = ocr.ImageRotationStatus,
            database = await db.Database.CanConnectAsync() ? "Ready" : "Unavailable",
            scanner,
            smtp,
            storage = config["Storage:Provider"] ?? "Local",
            ocrProvider = "Tesseract",
            ocrLanguages = config["Ocr:Languages"] ?? "ron+eng",
            queuedJobs = await db.Jobs.CountAsync(j => j.State == "Pending" || j.State == "Processing"),
            failedJobs = await db.Jobs.CountAsync(j => j.State == "Failed"),
            schemaVersion = await db.SchemaReleases.MaxAsync(r => (int?)r.Id) ?? 0,
        };
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/admin/health", Dependencies).RequireAuthorization(p => p.RequireRole("Admin"));
        api.MapGet(
            "/reports/{id:guid}/jobs",
            async (Guid id, Db db, Reports svc, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                var ids = r
                    .Attachments.Select(e => e.Id.ToString())
                    .Concat(r.Lines.SelectMany(l => l.Evidence).Select(e => e.Id.ToString()))
                    .ToList();
                var events = await db
                    .DocumentEvents.Where(e => e.ReportId == id)
                    .Select(e => e.Id.ToString())
                    .ToListAsync();
                var jobs = await db
                    .Jobs.Where(j =>
                        ids.Contains(j.Json)
                        || j.Kind == "DocumentEvent" && events.Contains(j.Json)
                        || j.Kind == "Email" && j.Json.Contains(id.ToString())
                    )
                    .ToListAsync();
                return jobs.Select(j => new
                {
                    j.Id,
                    j.Kind,
                    j.State,
                    j.Attempts,
                    j.Due,
                    j.StartedAt,
                    j.CompletedAt,
                    j.FailureCode,
                    error = j.State == "Failed"
                        ? "Operațiunea a eșuat; administratorul poate reîncerca. / Operation failed; an administrator can retry."
                        : null,
                });
            }
        );
    }
}
