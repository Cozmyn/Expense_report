using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
namespace Decont;

public class DocumentEvent
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public Guid DocumentId { get; set; }
    public string Type { get; set; } = ""; public string Actor { get; set; } = ""; public string Payload { get; set; } = "{}"; public DateTime Created { get; set; } = DateTime.UtcNow;
}
public static class DocumentEvents
{
    // The event row and its dispatch job are saved in the caller's transaction.
    public static void Emit(Db db, Guid report, Guid document, string type, string actor, string semanticKey, object metadata)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(type + "|" + report + "|" + document + "|" + semanticKey)); var id = new Guid(hash.AsSpan(0, 16)); if (db.DocumentEvents.Local.Any(e => e.Id == id) || db.DocumentEvents.Any(e => e.Id == id)) return;
        db.DocumentEvents.Add(new() { Id = id, ReportId = report, DocumentId = document, Type = type, Actor = actor, Payload = Serialization.Write(metadata) }); db.Jobs.Add(new() { Id = id, Kind = "DocumentEvent", Json = id.ToString() });
    }
    public static async Task Deliver(Guid id, Db db, CancellationToken ct)
    {
        var entry = await db.DocumentEvents.SingleAsync(e => e.Id == id, ct); if (entry.Type is not ("ExtractionCompleted" or "OcrJobFailed")) return;
        using var payload = System.Text.Json.JsonDocument.Parse(entry.Payload); if (entry.Type == "ExtractionCompleted" && payload.RootElement.TryGetProperty("reviewRequired", out var required) && !required.GetBoolean()) return;
        var report = await db.Reports.FindAsync([entry.ReportId], ct); if (report == null) return; var source = await db.ReportAttachments.FindAsync([entry.DocumentId], ct); if (source?.Archived == true) return; var requestedBy = await db.Audits.Where(a => a.ReportId == entry.ReportId && a.Entity == entry.DocumentId.ToString() && a.Action == "ExtractionRequested").OrderByDescending(a => a.At).Select(a => a.Actor).FirstOrDefaultAsync(ct); var target = requestedBy ?? (!string.IsNullOrWhiteSpace(source?.UploadedBy) ? source.UploadedBy : report.EmployeeId); var user = await db.Users.FindAsync([target], ct); if (user == null || !user.Active) return;
        if (entry.Type == "ExtractionCompleted") { var review = await db.DocumentReviews.FindAsync([entry.DocumentId], ct); if (review != null) { var groups = await db.DocumentGroups.Where(g => g.BundleId == review.Id).ToListAsync(ct); if (OcrReviews.Unassigned(review, groups).Length == 0 && groups.Where(OcrReviews.Active).All(g => g.State == "Verified")) return; } }
        var roles = await db.UserRoles.Join(db.Roles, r => r.RoleId, r => r.Id, (u, r) => new { u.UserId, r.Name }).Where(r => r.UserId == target).Select(r => r.Name).ToListAsync(ct); if (!(roles.Contains("Employee") && report.EmployeeId == target && States.EmployeeEdit(report.State) || roles.Contains("Finance") && States.FinanceEdit(report.State))) return; var channels = await NotificationPolicies.Channels(db, entry.Type); if (channels.Item1 && !await db.Notices.AnyAsync(n => n.Id == entry.Id, ct)) db.Notices.Add(new() { Id = entry.Id, ReportId = entry.ReportId, UserId = target, Event = entry.Type == "ExtractionCompleted" ? "OcrReviewReady" : "OcrProcessingFailed" });
    }
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reports/{id:guid}/documents/{document:guid}/events", async (Guid id, Guid document, Db db, Reports svc, HttpContext c) =>
        {
            var report = await svc.Get(id, c.User); Guard.Require(report.Attachments.Any(a => a.Id == document) || report.Lines.Any(l => l.Evidence.Any(e => e.Id == document)), "missing", "Document inexistent.", "Document not found.", 404);
            return await db.DocumentEvents.Where(e => e.ReportId == id && e.DocumentId == document).OrderBy(e => e.Created).Select(e => new { e.Id, e.Type, e.Created, e.Actor, metadata = e.Payload, delivery = db.Jobs.Where(j => j.Id == e.Id).Select(j => j.State).FirstOrDefault(), attempts = db.Jobs.Where(j => j.Id == e.Id).Select(j => j.Attempts).FirstOrDefault() }).ToListAsync();
        });
    }
}
