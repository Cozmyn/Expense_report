using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class DocumentStatus
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
            "/reports/{id:guid}/document-status",
            async (Guid id, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                var documents = report.Attachments.Where(a => !a.Archived).ToList();
                var ids = documents.Select(a => a.Id).ToList();
                var reviews = await db.DocumentReviews.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
                var groups = await db.DocumentGroups.Where(g => ids.Contains(g.BundleId)).ToListAsync();
                var strings = ids.Select(x => x.ToString()).ToList();
                var jobs = await db
                    .Jobs.Where(j => (j.Kind == "Ocr" || j.Kind == "OcrReport") && strings.Contains(j.Json))
                    .ToListAsync();
                List<object> rows = [];
                foreach (var document in documents.Where(d => d.Capture || reviews.ContainsKey(d.Id)))
                {
                    reviews.TryGetValue(document.Id, out var review);
                    var active = groups.Where(g => g.BundleId == document.Id && OcrReviews.Active(g)).ToList();
                    var unassigned = review == null ? [] : OcrReviews.Unassigned(review, active);
                    using var json = JsonDocument.Parse(document.Ocr ?? "{}");
                    var metadata = json.RootElement;
                    string Text(string key) =>
                        metadata.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                            ? v.GetString() ?? ""
                            : "";
                    int Number(string key) =>
                        metadata.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : 0;
                    var total = review?.PageCount ?? Number("totalPages");
                    var complete = Number("completedPages");
                    var failed = Number("failedPages");
                    if (metadata.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                    {
                        complete = pages.GetArrayLength();
                        failed = pages
                            .EnumerateArray()
                            .Count(p =>
                                p.TryGetProperty("error", out var error)
                                && error.ValueKind == JsonValueKind.String
                                && error.GetString() == "ExtractionFailed"
                            );
                    }
                    var state =
                        document.Scan != "Clean" ? document.Scan
                        : review == null ? Text("state")
                        : unassigned.Length > 0 || active.Any(g => g.State != "Verified") ? "ReviewRequired"
                        : "Verified";
                    rows.Add(
                        new
                        {
                            id = document.Id,
                            state,
                            stage = Text("stage"),
                            totalPages = total,
                            completedPages = complete,
                            failedPages = failed,
                            unassignedPages = unassigned,
                            groups = active.Count,
                            verifiedGroups = active.Count(g => g.State == "Verified"),
                            attempts = jobs.Where(j => j.Json == document.Id.ToString()).Sum(j => j.Attempts),
                        }
                    );
                }
                return rows;
            }
        );
        api.MapGet(
                "/admin/ocr-metrics",
                async (Db db, Reports svc, HttpContext c) =>
                {
                    var reports = svc.Scope(c.User).Select(r => r.Id);
                    var reviews = db.DocumentReviews.Where(r => reports.Contains(r.ReportId));
                    var reviewIds = reviews.Select(r => r.Id);
                    var groups = db.DocumentGroups.Where(g => reviewIds.Contains(g.BundleId));
                    var events = db.DocumentEvents.Where(e => reports.Contains(e.ReportId));
                    var eventIds = await events.Select(e => e.Id).ToListAsync();
                    return new
                    {
                        documents = await reviews.CountAsync(),
                        sourcePages = await reviews.SumAsync(r => (int?)r.PageCount) ?? 0,
                        verifiedGroups = await groups.CountAsync(g => g.State == "Verified"),
                        needsReview = await groups.CountAsync(g => g.State == "ReviewRequired"),
                        quarantined = await db.ReportAttachments.CountAsync(a =>
                            reports.Contains(a.ReportId) && a.Scan == "Rejected"
                        ),
                        queuedJobs = await db.Jobs.CountAsync(j =>
                            (j.Kind == "Ocr" || j.Kind == "OcrReport")
                            && (j.State == "Pending" || j.State == "Processing")
                        ),
                        failedJobs = await db.Jobs.CountAsync(j =>
                            (j.Kind == "Ocr" || j.Kind == "OcrReport") && j.State == "Failed"
                        ),
                        outboxPending = await db.Jobs.CountAsync(j =>
                            eventIds.Contains(j.Id) && j.State != "Completed"
                        ),
                        verifiedEvents = await events.CountAsync(e => e.Type == "DocumentVerified"),
                        correctionEvents = await events.CountAsync(e => e.Type == "HumanCorrectionApplied"),
                    };
                }
            )
            .RequireAuthorization(p => p.RequireRole("Admin", "Finance", "Auditor"));
    }
}
