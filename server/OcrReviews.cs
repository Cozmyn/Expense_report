using Microsoft.EntityFrameworkCore;

namespace Decont;

public class DocumentReview
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string OriginalExtraction { get; set; } = "";
    public int PageCount { get; set; }
    public string Dispositions { get; set; } = "[]";
    public DateTime Created { get; set; } = DateTime.UtcNow;
}

public class DocumentGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BundleId { get; set; }
    public string SourceKey { get; set; } = "";
    public string State { get; set; } = "ReviewRequired";
    public string Regions { get; set; } = "[]";
    public string Fields { get; set; } = "{}";
    public string Taxes { get; set; } = "[]";
    public string Warnings { get; set; } = "[]";
    public string Parents { get; set; } = "[]";
    public Guid? ExpenseId { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? CorrectedExpense { get; set; }
}

public class ExtractionAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BundleId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Json { get; set; } = "";
    public DateTime Created { get; set; } = DateTime.UtcNow;
}

public record SourceRegion(int Page, decimal X = 0, decimal Y = 0, decimal Width = 1, decimal Height = 1);

// Keep completeness acknowledgements separate from supporting-page disposition.
// The optional Kind preserves compatibility with existing serialized review JSON.
// Coverage entries use Page=0 and CoveragePage=the physical page.
// This fails safely if an older version is restored: it cannot accidentally
// treat an acknowledgement as a supporting disposition for a real page.
public record PageDisposition(
    int Page, string Reason, string Kind = "Supporting", int? CoveragePage = null);

public record GroupCommand(
    long Version,
    string Action,
    Guid[]? Groups = null,
    SourceRegion[]? Regions = null,
    int[]? Pages = null,
    string? Reason = null
);

public record OcrDraftCommand(long Version, ExpenseData Data, string? Reason = null);

public record PageCommand(long Version, int Page, bool Supporting, string Reason);
public record PageCoverageCommand(long Version, int Page, bool Complete, string Reason);

public static class OcrReviews
{
    public static bool Active(DocumentGroup g) => g.State is "ReviewRequired" or "Verified";

    public static List<SourceRegion> Regions(DocumentGroup g) => Serialization.Read<List<SourceRegion>>(g.Regions);

    public static bool Overlap(SourceRegion a, SourceRegion b) =>
        a.Page == b.Page && a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    public static void ValidateRegions(IEnumerable<SourceRegion> regions, int pages)
    {
        var all = regions.ToList();
        Guard.Require(
            all.Count > 0
                && all.Count <= 100
                && all.All(r =>
                    r.Page >= 1
                    && r.Page <= pages
                    && r.X >= 0
                    && r.Y >= 0
                    && r.Width > 0
                    && r.Height > 0
                    && r.X + r.Width <= 1
                    && r.Y + r.Height <= 1
                ),
            "region",
            "Selectează pagini și regiuni valide.",
            "Select valid pages and regions."
        );
    }

    public static async Task Import(Db db, Guid report, Guid document, OcrResult result)
    {
        if (result.Pages.Count == 0)
            return;
        var reportEntity = await db.Reports.FindAsync(report);
        var editable =
            reportEntity != null && (States.EmployeeEdit(reportEntity.State) || States.FinanceEdit(reportEntity.State));
        // Keep the original extraction immutable for audit. Supplier hints affect only
        // new, unverified group suggestions and never overwrite prior verified data.
        var suggested = editable ? await SupplierOcrLearning.Suggest(db, result) : result;
        var json = Serialization.Write(result);
        var fingerprint = Serialization.Hash(json);
        var review = await db.DocumentReviews.FindAsync(document);
        bool created = false;
        if (review == null)
        {
            created = true;
            if (!editable)
                return;
            review = new()
            {
                Id = document,
                ReportId = report,
                OriginalExtraction = json,
                PageCount = result.Pages.Max(p => p.Number),
            };
            db.DocumentReviews.Add(review);
            // A single-receipt upload is represented by one review group even if
            // its PDF has several pages or parsing did not identify a total.
            // Keep the unmodified extraction and review evidence for audit.
            var receiptMode = await db.ReportAttachments.Where(a => a.Id == document)
                .Select(a => a.ReceiptMode).SingleAsync();
            if (receiptMode == "Single")
            {
                var parsed = suggested.Candidates
                    .OrderByDescending(c => c.Fields.Count(f => !string.IsNullOrWhiteSpace(f.Value.Value)))
                    .ThenBy(c => c.Pages.Min())
                    .ToList();
                var fields = new Dictionary<string, OcrField>();
                foreach (var candidate in parsed)
                foreach (var field in candidate.Fields)
                    if (!fields.TryGetValue(field.Key, out var existing)
                        || (string.IsNullOrWhiteSpace(existing.Value) && !string.IsNullOrWhiteSpace(field.Value.Value)))
                        fields[field.Key] = field.Value;
                // A single-upload containing multiple detected receipts is unresolved.
                // Never combine unrelated receipts or VAT breakdowns into one draft.
                var taxes = parsed.Count == 1 ? parsed[0].Taxes.ToList() : [];
                var warnings = parsed.SelectMany(c => c.Warnings).Distinct().ToList();
                if (parsed.Count > 1)
                {
                    fields.Clear();
                    warnings.Add("MULTIPLE_SECTIONS_SINGLE: Au fost găsite mai multe bonuri într-un fișier ales ca bon unic. Alege «Separă bonurile» înainte de salvare. / Several receipts were found in a single-receipt upload. Choose «Separate receipts» before saving.");
                }
                if (parsed.Count == 0)
                    warnings.Add("Nu au fost identificate câmpuri. Selectează valorile din document sau completează manual. / No fields identified. Select values from the document or enter them manually.");
                db.DocumentGroups.Add(new()
                {
                    BundleId = document,
                    SourceKey = "single:" + document.ToString("N"),
                    Regions = Serialization.Write(result.Pages.Select(p => new SourceRegion(p.Number)).Distinct().ToList()),
                    Fields = Serialization.Write(fields),
                    Taxes = Serialization.Write(taxes),
                    Warnings = Serialization.Write(warnings),
                });
            }
            else
            {
                foreach (var c in suggested.Candidates)
                {
                    db.DocumentGroups.Add(
                        new()
                        {
                            BundleId = document,
                            SourceKey = c.Id.ToString(),
                            Regions = Serialization.Write(c.Regions ?? c.Pages.Select(p => new SourceRegion(p)).ToList()),
                            Fields = Serialization.Write(c.Fields),
                            Taxes = Serialization.Write(c.Taxes),
                            Warnings = Serialization.Write(c.Warnings),
                        }
                    );
                }
            }
        }
        else if (editable)
        {
            var existing = (await db.DocumentGroups.Where(g => g.BundleId == document).ToListAsync())
                .Concat(db.DocumentGroups.Local.Where(g => g.BundleId == document))
                .DistinctBy(g => g.Id)
                .ToList();
            var supporting = Serialization
                .Read<List<PageDisposition>>(review.Dispositions)
                .Select(p => p.Page)
                .ToHashSet();
            bool added = false;
            foreach (var c in suggested.Candidates)
            {
                var regions = c.Regions ?? c.Pages.Select(p => new SourceRegion(p)).ToList();
                if (
                    existing.Any(g => g.SourceKey == c.Id.ToString())
                    || regions.Any(r => supporting.Contains(r.Page))
                    || existing.Where(Active).Any(g => Regions(g).Any(a => regions.Any(b => Overlap(a, b))))
                )
                    continue;
                var group = new DocumentGroup
                {
                    BundleId = document,
                    SourceKey = c.Id.ToString(),
                    Regions = Serialization.Write(regions),
                    Fields = Serialization.Write(c.Fields),
                    Taxes = Serialization.Write(c.Taxes),
                    Warnings = Serialization.Write(c.Warnings),
                };
                db.DocumentGroups.Add(group);
                existing.Add(group);
                added = true;
            }
            if (added && reportEntity != null)
            {
                // New OCR candidates invalidate previous visual completeness checks.
                review.Dispositions = Serialization.Write(
                    Serialization.Read<List<PageDisposition>>(review.Dispositions)
                        .Where(p => p.Kind != "CoverageConfirmed").ToList()
                );
                Reports.Touch(reportEntity);
            }
        }
        // Raw attempts are append-only; retry never overwrites corrected/verified groups.
        if (created && reportEntity != null)
            Reports.Touch(reportEntity);
        if (
            !db.ExtractionAttempts.Local.Any(a => a.BundleId == document && a.Fingerprint == fingerprint)
            && !await db.ExtractionAttempts.AnyAsync(a => a.BundleId == document && a.Fingerprint == fingerprint)
        )
        {
            db.ExtractionAttempts.Add(
                new()
                {
                    BundleId = document,
                    Fingerprint = fingerprint,
                    Json = json,
                }
            );
            DocumentEvents.Emit(
                db,
                report,
                document,
                "ExtractionCompleted",
                "system",
                fingerprint,
                new
                {
                    pages = result.Pages.Count,
                    groups = result.Candidates.Count,
                    failedPages = result.Pages.Count(p => p.Error != null),
                    reviewRequired = editable,
                }
            );
            if (created)
                DocumentEvents.Emit(
                    db,
                    report,
                    document,
                    "DocumentSegmented",
                    "system",
                    fingerprint,
                    new { groups = result.Candidates.Count, unassigned = result.UnassignedPages?.Length ?? 0 }
                );
        }
    }

    public static int[] Unassigned(DocumentReview review, IEnumerable<DocumentGroup> groups)
    {
        var assigned = groups
            .Where(Active)
            .SelectMany(Regions)
            .Select(r => r.Page)
            .Concat(Serialization.Read<List<PageDisposition>>(review.Dispositions)
                .Where(p => p.Kind != "CoverageConfirmed").Select(p => p.Page))
            .ToHashSet();
        return Enumerable.Range(1, review.PageCount).Where(p => !assigned.Contains(p)).ToArray();
    }

    public static bool NeedsPageCoverage(ReportAttachment source, IEnumerable<DocumentGroup> groups) =>
        source.ReceiptMode == "Multiple"
        || groups.Any(g => g.SourceKey.StartsWith("single:") && g.State == "Split");

    public static int[] ConfirmedPages(DocumentReview review) =>
        Serialization.Read<List<PageDisposition>>(review.Dispositions)
            .Where(d => d.Kind == "CoverageConfirmed")
            .Select(d => d.CoveragePage ?? d.Page).Distinct().Order().ToArray();

    public static async Task<object> View(Db db, DocumentReview review)
    {
        var groups = await db.DocumentGroups.Where(g => g.BundleId == review.Id).ToListAsync();
        var latest = await db
            .ExtractionAttempts.Where(a => a.BundleId == review.Id)
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync();
        var source = Serialization.Read<OcrResult>(latest?.Json ?? review.OriginalExtraction);
        var original = Serialization.Read<OcrResult>(review.OriginalExtraction);
        var report = await db.Reports.SingleAsync(r => r.Id == review.ReportId);
        var masters = await db.Masters.ToListAsync();
        var hash = await db.ReportAttachments.Where(a => a.Id == review.Id).Select(a => a.Hash).SingleAsync();
        var duplicate = await db.ReportAttachments.AnyAsync(a => a.Id != review.Id && a.Hash == hash && !a.Archived);
        var sourceFile = await db.ReportAttachments.SingleAsync(a => a.Id == review.Id);
        return new
        {
            review.Id,
            review.PageCount,
            version = report.Version,
            duplicateUpload = duplicate,
            pageStates = source.Pages.Select(p => new { p.Number, p.Error }),
            tombstones = groups.Where(g => !Active(g)).Select(g => new { g.Id, g.State }),
            unassignedPages = Unassigned(review, groups),
            coverageRequired = NeedsPageCoverage(sourceFile, groups),
            confirmedPages = ConfirmedPages(review),
            dispositions = Serialization.Read<List<PageDisposition>>(review.Dispositions)
                .Where(d => d.Kind != "CoverageConfirmed").ToList(),
            candidates = groups
                .Where(Active)
                .OrderBy(g => Regions(g).Min(r => r.Page))
                .ThenBy(g => Regions(g).First().Y)
                .ThenBy(g => g.Id)
                .Select(g => new
                {
                    g.Id,
                    g.State,
                    g.ExpenseId,
                    correctedExpense = g.CorrectedExpense == null
                        ? null
                        : Serialization.Read<ExpenseData>(g.CorrectedExpense),
                    pages = Regions(g).Select(r => r.Page).Distinct().Order().ToArray(),
                    regions = Regions(g),
                    parents = Serialization.Read<List<Guid>>(g.Parents),
                    groupingReason = original
                        .Candidates.FirstOrDefault(c => c.Id.ToString() == g.SourceKey)
                        ?.GroupingReason
                        ?? (g.SourceKey.StartsWith("single:")
                            ? "Single receipt chosen by employee"
                            : "Human regrouping; review source history"),
                    boundaryConfidence = original
                        .Candidates.FirstOrDefault(c => c.Id.ToString() == g.SourceKey)
                        ?.BoundaryConfidence,
                    alternatives = original
                        .Candidates.Where(c =>
                            (c.Regions ?? c.Pages.Select(p => new SourceRegion(p)).ToList()).Any(a =>
                                Regions(g).Any(b => Overlap(a, b))
                            )
                        )
                        .Select(c => new
                        {
                            c.Pages,
                            c.Fields,
                            c.Taxes,
                            c.Alternatives,
                            c.TaxAlternatives,
                        }),
                    categorySuggestions = DocumentAssistance.Categories(
                        DocumentAssistance.Evidence(original, Regions(g)),
                        masters,
                        DateOnly.TryParse(
                            Serialization.Read<Dictionary<string, OcrField>>(g.Fields).GetValueOrDefault("date")?.Value,
                            out var date
                        )
                            ? date
                            : DateOnly.FromDateTime(DateTime.UtcNow)
                    ),
                    defaults = DocumentAssistance.Defaults(
                        masters,
                        report,
                        DateOnly.TryParse(
                            Serialization.Read<Dictionary<string, OcrField>>(g.Fields).GetValueOrDefault("date")?.Value,
                            out var allocationDate
                        )
                            ? allocationDate
                            : DateOnly.FromDateTime(DateTime.UtcNow)
                    ),
                    fields = Serialization.Read<Dictionary<string, OcrField>>(g.Fields),
                    taxes = Serialization.Read<List<OcrTax>>(g.Taxes),
                    warnings = Serialization.Read<List<string>>(g.Warnings),
                }),
            historyCount = await db.Audits.CountAsync(a => a.Entity == review.Id.ToString()),
            attempts = await db.ExtractionAttempts.CountAsync(a => a.BundleId == review.Id),
            activeAttemptId = latest?.Id,
        };
    }

    public static async Task<List<Problem>> Problems(Db db, Report report)
    {
        var ids = report.Attachments.Where(a => !a.Archived).Select(a => a.Id).ToList();
        var reviews = await db.DocumentReviews.Where(r => ids.Contains(r.Id)).ToListAsync();
        List<Problem> issues = [];
        foreach (
            var source in report.Attachments.Where(a => !a.Archived && a.Capture && !reviews.Any(v => v.Id == a.Id))
        )
            issues.Add(
                new(
                    "ocr_review_required",
                    "Documentul încărcat necesită verificarea paginilor înainte de trimitere.",
                    "Uploaded document requires page review before submission."
                )
            );
        foreach (var review in reviews)
        {
            var groups = await db.DocumentGroups.Where(g => g.BundleId == review.Id).ToListAsync();
            if (Unassigned(review, groups).Length > 0)
                issues.Add(
                    new(
                        "ocr_unassigned_pages",
                        "Verifică paginile neatribuite din documentele încărcate.",
                        "Review unassigned pages in uploaded documents."
                    )
                );
            var source = report.Attachments.Single(a => a.Id == review.Id);
            if (NeedsPageCoverage(source, groups)
                && Enumerable.Range(1, review.PageCount).Except(ConfirmedPages(review)).Any())
                issues.Add(
                    new(
                        "ocr_page_coverage_required",
                        "Confirmă vizual că toate bonurile de pe fiecare pagină au fost adăugate.",
                        "Visually confirm that every receipt on every page is accounted for."
                    )
                );
            if (
                groups
                    .Where(Active)
                    .Any(g =>
                        g.State != "Verified"
                        || g.ExpenseId == null
                        || !report.Lines.Any(l => !l.Archived && l.Id == g.ExpenseId)
                    )
            )
                issues.Add(
                    new(
                        "ocr_unverified_groups",
                        "Verifică fiecare document OCR sau marchează-l justificativ cu motiv.",
                        "Verify each OCR document or mark it as supporting evidence with a reason."
                    )
                );
        }
        return issues;
    }

    public static async Task Verify(
        Db db,
        Reports svc,
        Report report,
        Line line,
        Guid document,
        Guid groupId,
        HttpContext context,
        string? reason
    )
    {
        Guard.Require(
            report.Attachments.Any(a => a.Id == document && !a.Archived),
            "source",
            "Document sursă indisponibil.",
            "Source document unavailable.",
            404
        );
        var group = await db.DocumentGroups.SingleOrDefaultAsync(g => g.Id == groupId && g.BundleId == document);
        Guard.Require(
            group != null && Active(group),
            "ocr_group",
            "Grupul sursă nu este disponibil.",
            "Source group is unavailable."
        );
        Guard.Require(
            group!.ExpenseId == null || group.ExpenseId == line.Id,
            "ocr_consumed",
            "Documentul a creat deja un bon. Deschide bonul existent.",
            "This document already created a receipt. Open the existing receipt.",
            409
        );
        var review = await db.DocumentReviews.SingleAsync(r => r.Id == document && r.ReportId == report.Id);
        var source = report.Attachments.Single(a => a.Id == document && !a.Archived);
        Guard.Require(source.Scan == "Clean", "scan", "Așteaptă scanarea documentului.", "Wait for document scanning.");
        Guard.Require(
            source.ReceiptMode != "Single"
            || !Serialization.Read<List<string>>(group.Warnings).Any(w => w.StartsWith("MULTIPLE_SECTIONS_SINGLE:")),
            "ocr_multiple_receipts",
            "Au fost detectate mai multe bonuri. Separă-le înainte de confirmare.",
            "Several receipts were detected. Separate them before verifying.",
            409
        );
        var regions = Regions(group);
        ValidateRegions(regions, review.PageCount);
        var others = await db
            .DocumentGroups.Where(g => g.BundleId == document && g.Id != group.Id && g.State == "Verified")
            .ToListAsync();
        Guard.Require(
            !others.Any(g => Regions(g).Any(a => regions.Any(b => Overlap(a, b)))),
            "region_overlap",
            "Regiunile se suprapun cu un bon verificat. Corectează delimitarea.",
            "Regions overlap a verified receipt. Correct the boundaries."
        );
        var duplicateBundles = await db
            .ReportAttachments.Where(a => a.Hash == source.Hash && a.Id != document && !a.Archived)
            .Select(a => a.Id)
            .ToListAsync();
        var duplicateGroups = await db
            .DocumentGroups.Where(g => duplicateBundles.Contains(g.BundleId) && g.State == "Verified")
            .ToListAsync();
        Guard.Require(
            !duplicateGroups.Any(g => Regions(g).Any(a => regions.Any(b => Overlap(a, b)))),
            "duplicate_source",
            "Aceeași regiune sursă a fost deja verificată dintr-o încărcare duplicată.",
            "This source region was already verified from a duplicate upload.",
            409
        );
        DocumentCapture.Link(report, line, document, svc, context, reason);
        var check = new Report
        {
            Id = report.Id,
            Purpose = report.Purpose,
            EmployeeId = report.EmployeeId,
            ManagerId = report.ManagerId,
            Payment = report.Payment,
            CardId = report.CardId,
            Lines = [line],
            Attachments = [source],
        };
        var masterList = await db.Masters.ToListAsync();
        var issues = Rules.Validate(check, masterList);
        // OCR confirmation is a RECEIPT check. The manager is selected from the
        // final cost-centre totals only when the report is submitted. Do not
        // prevent recording valid evidence while the owner route is pending.
        var receiptIssues = issues.Where(p => p.Blocking && p.Code != "manager").ToList();
        Guard.Require(
            receiptIssues.Count == 0,
            "ocr_validation",
            Serialization.Write(receiptIssues),
            Serialization.Write(receiptIssues)
        );
        var before = new
        {
            group.State,
            group.CorrectedExpense,
            group.ExpenseId,
        };
        group.State = "Verified";
        group.ExpenseId = line.Id;
        group.VerifiedBy = Reports.Actor(context.User);
        group.VerifiedAt = DateTime.UtcNow;
        group.CorrectedExpense = line.Json;
        DocumentEvents.Emit(
            db,
            report.Id,
            document,
            "DocumentVerified",
            Reports.Actor(context.User),
            group.Id + "|" + report.Version,
            new
            {
                group.Id,
                group.ExpenseId,
                pages = regions.Select(r => r.Page).Distinct().ToArray(),
            }
        );
        var finalFields = System.Text.Json.JsonSerializer.SerializeToElement(line.Data(), Serialization.Options);
        var rawFields = Serialization.Read<Dictionary<string, OcrField>>(group.Fields);
        var rawTaxes = Serialization.Read<List<OcrTax>>(group.Taxes);
        var finalTaxes = line.Data()
            .Taxes.Select(t => new
            {
                rate = Rules.Number(masterList.Single(m => m.Id == t.RateId), "rate"),
                t.Base,
                t.Vat,
            })
            .ToList();
        var changed =
            before.CorrectedExpense != null
                ? before.CorrectedExpense != group.CorrectedExpense
                : rawFields.Any(f => !Same(f.Value.Value, Value(finalFields, f.Key), f.Key))
                    || rawTaxes
                        .Select(t => t.Rate)
                        .Concat(finalTaxes.Select(t => t.rate))
                        .Distinct()
                        .Any(rate =>
                            rawTaxes.Where(t => t.Rate == rate).Sum(t => t.Base)
                                != finalTaxes.Where(t => t.rate == rate).Sum(t => t.Base)
                            || rawTaxes.Where(t => t.Rate == rate).Sum(t => t.Vat)
                                != finalTaxes.Where(t => t.rate == rate).Sum(t => t.Vat)
                        );
        if (changed)
            DocumentEvents.Emit(
                db,
                report.Id,
                document,
                "HumanCorrectionApplied",
                Reports.Actor(context.User),
                group.Id + "|" + report.Version,
                new { group.Id, group.ExpenseId }
            );
        svc.Log(
            context.User,
            report.Id,
            document.ToString(),
            "DocumentVerified",
            before,
            new
            {
                group.Id,
                group.ExpenseId,
                group.VerifiedAt,
                group.CorrectedExpense,
                regions,
                verifiedTaxes = line.Data()
                    .Taxes.Select(t => new
                    {
                        rate = Rules.Number(masterList.Single(m => m.Id == t.RateId), "rate"),
                        t.Base,
                        t.Vat,
                    }),
            },
            reason
        );
    }

    static string? Value(System.Text.Json.JsonElement data, string key)
    {
        if (!data.TryGetProperty(key, out var value) || value.ValueKind == System.Text.Json.JsonValueKind.Null)
            return null;
        return value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : value.ToString();
    }

    static bool Same(string? a, string? b, string key)
    {
        if (a == b)
            return true;
        if (
            (key == "amount" || key.StartsWith("vat.", StringComparison.Ordinal))
            && decimal.TryParse(
                a,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var x
            )
            && decimal.TryParse(
                b,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var y
            )
        )
            return x == y;
        if (key == "hasCompanyCui" && bool.TryParse(a, out var first) && bool.TryParse(b, out var second))
            return first == second;
        return false;
    }

    public static async Task<object> Feedback(Db db, Guid report, Guid document)
    {
        var groups = await db.DocumentGroups.Where(g => g.BundleId == document).ToDictionaryAsync(g => g.Id);
        var events = await db
            .Audits.Where(a =>
                a.ReportId == report && a.Entity == document.ToString() && a.Action == "DocumentVerified"
            )
            .OrderBy(a => a.At)
            .ToListAsync();
        List<object> rows = [];
        var masters = await db.Masters.Where(m => m.Kind == "vat").ToListAsync();
        Dictionary<Guid, List<OcrTax>> previousTaxes = [];
        foreach (var entry in events)
        {
            if (entry.After == null)
                continue;
            using var after = System.Text.Json.JsonDocument.Parse(entry.After);
            var data = after.RootElement;
            var groupId = data.GetProperty("id").GetGuid();
            if (!groups.TryGetValue(groupId, out var group))
                continue;
            var corrected = data.GetProperty("correctedExpense").GetString();
            if (corrected == null)
                continue;
            using var verified = System.Text.Json.JsonDocument.Parse(corrected);
            System.Text.Json.JsonDocument? beforeDocument = null;
            try
            {
                if (entry.Before != null)
                {
                    using var before = System.Text.Json.JsonDocument.Parse(entry.Before);
                    var text = Value(before.RootElement, "correctedExpense");
                    if (text != null)
                        beforeDocument = System.Text.Json.JsonDocument.Parse(text);
                }
                var raw = Serialization.Read<Dictionary<string, OcrField>>(group.Fields);
                List<object> fields = [];
                foreach (
                    var key in new[]
                    {
                        "supplier",
                        "cui",
                        "invoice",
                        "date",
                        "currency",
                        "amount",
                        "country",
                        "documentType",
                        "hasCompanyCui",
                    }
                )
                {
                    var source = raw.GetValueOrDefault(key);
                    var original = source?.Value;
                    var previous = beforeDocument != null ? Value(beforeDocument.RootElement, key) : original;
                    var final = Value(verified.RootElement, key);
                    if (!Same(previous, final, key))
                        fields.Add(
                            new
                            {
                                field = key,
                                raw = original,
                                previous,
                                verified = final,
                                evidence = source?.Evidence,
                                page = source?.Page,
                                box = source?.Box,
                                recognitionConfidence = source?.Confidence,
                            }
                        );
                }
                var originalTaxes = Serialization.Read<List<OcrTax>>(group.Taxes);
                var finalExpense = Serialization.Read<ExpenseData>(corrected);
                List<OcrTax> finalTaxes = data.TryGetProperty("verifiedTaxes", out var storedTaxes)
                    ? storedTaxes
                        .EnumerateArray()
                        .Select(t => new OcrTax(
                            t.GetProperty("rate").GetDecimal(),
                            t.GetProperty("base").GetDecimal(),
                            t.GetProperty("vat").GetDecimal(),
                            0,
                            "HumanVerified"
                        ))
                        .ToList()
                    : finalExpense
                        .Taxes.Select(t => new OcrTax(
                            masters.Where(m => m.Id == t.RateId).Select(m => Rules.Number(m, "rate")).FirstOrDefault(),
                            t.Base,
                            t.Vat,
                            0,
                            "HumanVerified"
                        ))
                        .ToList();
                var priorTaxes = previousTaxes.GetValueOrDefault(groupId) ?? originalTaxes;
                foreach (
                    var rate in originalTaxes
                        .Select(t => t.Rate)
                        .Concat(priorTaxes.Select(t => t.Rate))
                        .Concat(finalTaxes.Select(t => t.Rate))
                        .Distinct()
                        .Order()
                )
                {
                    var sourceRows = originalTaxes.Where(t => t.Rate == rate).ToList();
                    var oldRows = priorTaxes.Where(t => t.Rate == rate).ToList();
                    var finalRows = finalTaxes.Where(t => t.Rate == rate).ToList();
                    foreach (var part in new[] { "base", "vat" })
                    {
                        string? Total(List<OcrTax> rows) =>
                            rows.Count == 0
                                ? null
                                : rows.Sum(t => part == "base" ? t.Base : t.Vat)
                                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var rawValue = Total(sourceRows);
                        var oldValue = Total(oldRows);
                        var finalValue = Total(finalRows);
                        var key =
                            "vat." + rate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + part;
                        if (!Same(oldValue, finalValue, key))
                            fields.Add(
                                new
                                {
                                    field = key,
                                    raw = rawValue,
                                    previous = oldValue,
                                    verified = finalValue,
                                    evidence = string.Join("\n", sourceRows.Select(t => t.Evidence)),
                                    pages = sourceRows.Select(t => t.Page).Distinct().ToArray(),
                                    boxes = sourceRows.Where(t => t.Box != null).Select(t => new { t.Page, t.Box }),
                                    recognitionConfidence = sourceRows.Count > 0
                                    && sourceRows.All(t => t.Confidence != null)
                                        ? sourceRows.Min(t => t.Confidence)
                                        : null,
                                }
                            );
                    }
                }
                previousTaxes[groupId] = finalTaxes;
                rows.Add(
                    new
                    {
                        groupId,
                        expenseId = data.GetProperty("expenseId").GetGuid(),
                        entry.Actor,
                        entry.At,
                        fields,
                    }
                );
            }
            finally
            {
                beforeDocument?.Dispose();
            }
        }
        return rows;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPatch(
            "/reports/{id:guid}/documents/{document:guid}/groups/{group:guid}/fields",
            async (Guid id, Guid document, Guid group, OcrDraftCommand input, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    input,
                    async () =>
                    {
                        var report = await svc.Version(id, input.Version, c.User);
                        svc.Edit(report, c.User, input.Reason);
                        Guard.Require(
                            report.Attachments.Any(a => a.Id == document && !a.Archived && a.Scan == "Clean"),
                            "source",
                            "Document sursă indisponibil.",
                            "Source unavailable.",
                            404
                        );
                        var item = await db.DocumentGroups.SingleOrDefaultAsync(g =>
                            g.Id == group && g.BundleId == document
                        );
                        Guard.Require(
                            item != null && item.State == "ReviewRequired" && item.ExpenseId == null,
                            "group_state",
                            "Ciorna poate fi modificată doar înainte de verificare.",
                            "Draft can be edited only before verification.",
                            409
                        );
                        var json = Serialization.Write(input.Data);
                        Guard.Require(
                            json.Length <= 24000,
                            "size",
                            "Ciorna depășește limita.",
                            "Draft exceeds limits."
                        );
                        var before = item!.CorrectedExpense;
                        item.CorrectedExpense = json;
                        Reports.Touch(report);
                        if (before != json)
                            DocumentEvents.Emit(
                                db,
                                id,
                                document,
                                "HumanCorrectionApplied",
                                Reports.Actor(c.User),
                                item.Id + "|" + report.Version + "|draft",
                                new { item.Id, state = "ReviewRequired" }
                            );
                        svc.Log(
                            c.User,
                            id,
                            document.ToString(),
                            "OcrDraftCorrected",
                            before,
                            new { item.Id, item.CorrectedExpense },
                            input.Reason
                        );
                        return Api.View(report);
                    }
                )
        );
        api.MapGet(
            "/reports/{id:guid}/documents/{document:guid}/review",
            async (Guid id, Guid document, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                Guard.Require(
                    report.Attachments.Any(a => a.Id == document),
                    "missing",
                    "Document inexistent.",
                    "Document not found.",
                    404
                );
                var review = await db.DocumentReviews.SingleOrDefaultAsync(r => r.Id == document && r.ReportId == id);
                return review == null
                    ? Results.Ok(new { available = false })
                    : Results.Ok(new { available = true, review = await View(db, review) });
            }
        );
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/review/start",
            async (
                Guid id,
                Guid document,
                VersionRequest i,
                Db db,
                Reports svc,
                IStore store,
                IConfiguration config,
                HttpContext c
            ) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var report = await svc.Version(id, i.Version, c.User);
                        svc.Edit(report, c.User, i.Reason);
                        var source = report.Attachments.Single(a => a.Id == document && !a.Archived);
                        Guard.Require(
                            source.Scan == "Clean",
                            "ocr_pending",
                            "Așteaptă scanarea.",
                            "Wait for scanning."
                        );
                        if (await db.DocumentReviews.AnyAsync(r => r.Id == document))
                            return Api.View(report);
                        OcrResult? result = null;
                        if (source.Ocr != null)
                        {
                            using var json = System.Text.Json.JsonDocument.Parse(source.Ocr);
                            if (
                                json.RootElement.TryGetProperty("pages", out var pages)
                                && pages.ValueKind == System.Text.Json.JsonValueKind.Array
                                && pages.GetArrayLength() > 0
                            )
                                result = Serialization.Read<OcrResult>(source.Ocr);
                        }
                        if (result == null)
                        {
                            Guard.Reason(i.Reason);
                            var bytes = await store.Get(source.Key, c.RequestAborted);
                            Guard.Require(
                                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == source.Hash,
                                "integrity",
                                "Originalul a fost modificat.",
                                "Original integrity failed."
                            );
                            var count = await DocumentPages.Count(source.Mime, bytes, config, c.RequestAborted);
                            result = new(
                                "Manual",
                                "ManualEntryRequired",
                                "",
                                Enumerable
                                    .Range(1, count)
                                    .Select(p => new OcrPage(p, "", "ManualReviewRequired"))
                                    .ToList(),
                                []
                            );
                        }
                        await Import(db, id, document, result);
                        svc.Log(c.User, id, document.ToString(), "DocumentReviewStarted", reason: i.Reason);
                        return Api.View(report);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/groups",
            async (Guid id, Guid document, GroupCommand i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var report = await svc.Version(id, i.Version, c.User);
                        svc.Edit(report, c.User, i.Reason);
                        Guard.Reason(i.Reason);
                        Guard.Require(
                            report.Attachments.Any(a => a.Id == document && !a.Archived && a.Scan == "Clean"),
                            "source",
                            "Documentul sursă nu este disponibil.",
                            "Source document is unavailable."
                        );
                        var review = await db.DocumentReviews.SingleAsync(r => r.Id == document && r.ReportId == id);
                        var all = await db.DocumentGroups.Where(g => g.BundleId == document).ToListAsync();
                        var ids = i.Groups ?? [];
                        Guard.Require(
                            ids.Distinct().Count() == ids.Length,
                            "group",
                            "Selecție duplicată.",
                            "Duplicate selection."
                        );
                        var selected = all.Where(g => ids.Contains(g.Id)).ToList();
                        Guard.Require(
                            selected.Count == ids.Length
                                && selected.All(g =>
                                    g.State == "ReviewRequired"
                                    || i.Action == "restore" && (g.State is "Deleted" or "Supporting")
                                ),
                            "group_state",
                            "Poți modifica doar grupuri neverificate. Bonurile verificate se corectează din decont.",
                            "Only unverified groups can be regrouped. Edit verified receipts from the report."
                        );
                        object before = new
                        {
                            groups = selected
                                .Select(g => new
                                {
                                    g.Id,
                                    g.State,
                                    g.Regions,
                                    g.Fields,
                                    g.Taxes,
                                })
                                .ToList(),
                            review.Dispositions,
                        };
                        List<DocumentGroup> added = [];
                        var extraction = Serialization.Read<OcrResult>(review.OriginalExtraction);
                        void Populate(DocumentGroup group, List<SourceRegion> regions)
                        {
                            bool Contains(int page, SourceBox? box) =>
                                regions.Any(r =>
                                    r.Page == page
                                    && (
                                        box != null
                                            ? box.X >= r.X
                                                && box.Y >= r.Y
                                                && box.X + box.Width <= r.X + r.Width
                                                && box.Y + box.Height <= r.Y + r.Height
                                            : r.X == 0 && r.Y == 0 && r.Width == 1 && r.Height == 1
                                    )
                                );
                            Dictionary<string, OcrField> fields = [];
                            List<OcrTax> taxes = [];
                            foreach (
                                var candidate in extraction.Candidates.Where(c =>
                                    c.Pages.Any(p => regions.Any(r => r.Page == p))
                                )
                            )
                            {
                                foreach (var pair in candidate.Fields.Where(f => Contains(f.Value.Page, f.Value.Box)))
                                    fields[pair.Key] = pair.Value;
                                taxes.AddRange(candidate.Taxes.Where(t => Contains(t.Page, t.Box)));
                            }
                            group.Fields = Serialization.Write(fields);
                            group.Taxes = Serialization.Write(taxes.Distinct().ToList());
                            group.CorrectedExpense = null;
                        }
                        DocumentGroup New(List<SourceRegion> regions, DocumentGroup? source = null)
                        {
                            ValidateRegions(regions, review.PageCount);
                            var g = new DocumentGroup
                            {
                                BundleId = document,
                                SourceKey = Guid.NewGuid().ToString(),
                                Regions = Serialization.Write(regions),
                                Fields = source?.Fields ?? "{}",
                                Taxes = source?.Taxes ?? "[]",
                                Warnings = Serialization.Write(
                                    new[]
                                    {
                                        "Verifică toate valorile după delimitare. / Verify all fields after regrouping.",
                                    }
                                ),
                                Parents = Serialization.Write(ids),
                            };
                            Populate(g, regions);
                            added.Add(g);
                            db.DocumentGroups.Add(g);
                            return g;
                        }
                        if (i.Action == "expand")
                        {
                            Guard.Require(
                                selected.Count == 1 && all.Count(Active) == 1
                                && selected[0].SourceKey.StartsWith("single:")
                                && report.Attachments.Single(a => a.Id == document).ReceiptMode == "Single"
                                && selected[0].CorrectedExpense == null,
                                "group_state",
                                "Separarea este permisă numai înainte de verificarea bonului unic.",
                                "A single receipt can be separated only before verification."
                            );
                            var latest = await db.ExtractionAttempts.Where(a => a.BundleId == document)
                                .OrderByDescending(a => a.Created).FirstOrDefaultAsync();
                            var candidates = Serialization.Read<OcrResult>(latest?.Json ?? review.OriginalExtraction).Candidates;
                            Guard.Require(
                                candidates.Count > 0,
                                "ocr_missing_candidates",
                                "Nu există propuneri de separat. Adaugă bonurile prin delimitare manuală.",
                                "No detected receipts to separate. Add receipt regions manually."
                            );
                            selected[0].State = "Split";
                            foreach (var candidate in candidates)
                            {
                                var regions = candidate.Regions ?? candidate.Pages.Select(p => new SourceRegion(p)).ToList();
                                ValidateRegions(regions, review.PageCount);
                                var created = new DocumentGroup
                                {
                                    BundleId = document,
                                    SourceKey = candidate.Id.ToString(),
                                    Regions = Serialization.Write(regions),
                                    Fields = Serialization.Write(candidate.Fields),
                                    Taxes = Serialization.Write(candidate.Taxes),
                                    Warnings = Serialization.Write(candidate.Warnings),
                                    Parents = Serialization.Write(ids),
                                };
                                db.DocumentGroups.Add(created);
                                added.Add(created);
                            }
                            // Preserve the immutable original upload intent ("Single").
                            // The Split tombstone and child groups record the reviewed correction.
                        }
                        else if (i.Action == "restore")
                        {
                            Guard.Require(selected.Count > 0, "group", "Selectează un grup.", "Select a group.");
                            var dispositions = Serialization.Read<List<PageDisposition>>(review.Dispositions);
                            foreach (var g in selected)
                            {
                                g.State = "ReviewRequired";
                                var pages = Regions(g).Select(r => r.Page).ToHashSet();
                                dispositions.RemoveAll(d => pages.Contains(d.Page));
                            }
                            review.Dispositions = Serialization.Write(dispositions);
                        }
                        else if (i.Action == "add")
                        {
                            Guard.Require(
                                ids.Length == 0,
                                "group",
                                "Nu selecta grupuri pentru un document nou.",
                                "Do not select groups for a new document."
                            );
                            New((i.Regions ?? []).ToList());
                        }
                        else if (i.Action == "regions")
                        {
                            Guard.Require(selected.Count == 1, "group", "Selectează un grup.", "Select one group.");
                            ValidateRegions(i.Regions ?? [], review.PageCount);
                            selected[0].Regions = Serialization.Write(i.Regions);
                            Populate(selected[0], (i.Regions ?? []).ToList());
                        }
                        else if (i.Action == "merge")
                        {
                            Guard.Require(
                                selected.Count >= 2,
                                "group",
                                "Selectează cel puțin două grupuri.",
                                "Select at least two groups."
                            );
                            New(selected.SelectMany(Regions).Distinct().OrderBy(r => r.Page).ToList(), selected[0]);
                            foreach (var g in selected)
                                g.State = "Merged";
                        }
                        else if (i.Action == "split")
                        {
                            Guard.Require(selected.Count == 1, "group", "Selectează un grup.", "Select one group.");
                            var split = (i.Pages ?? []).ToHashSet();
                            var regions = Regions(selected[0]);
                            var a = regions.Where(r => split.Contains(r.Page)).ToList();
                            var b = regions.Where(r => !split.Contains(r.Page)).ToList();
                            Guard.Require(
                                a.Count > 0 && b.Count > 0,
                                "split",
                                "Alege o parte din paginile grupului.",
                                "Choose some of the group's pages."
                            );
                            New(a, selected[0]);
                            New(b, selected[0]);
                            selected[0].State = "Split";
                        }
                        else if (i.Action == "supporting" || i.Action == "delete")
                        {
                            Guard.Require(selected.Count > 0, "group", "Selectează un grup.", "Select a group.");
                            var dispositions = Serialization.Read<List<PageDisposition>>(review.Dispositions);
                            foreach (var g in selected)
                            {
                                g.State = i.Action == "supporting" ? "Supporting" : "Deleted";
                                if (i.Action == "supporting")
                                    foreach (var page in Regions(g).Select(r => r.Page).Distinct())
                                        if (!dispositions.Any(p => p.Page == page))
                                            dispositions.Add(new(page, i.Reason!));
                            }
                            review.Dispositions = Serialization.Write(dispositions);
                        }
                        else
                            throw new DomainException("action", "Acțiune invalidă.", "Invalid action.");
                        // Any regrouping may reveal a receipt previously outside the
                        // reviewed regions. The visual acknowledgement must be repeated.
                        review.Dispositions = Serialization.Write(
                            Serialization.Read<List<PageDisposition>>(review.Dispositions)
                                .Where(p => p.Kind != "CoverageConfirmed").ToList()
                        );
                        Reports.Touch(report);
                        svc.Log(
                            c.User,
                            id,
                            document.ToString(),
                            "DocumentGroupsChanged",
                            before,
                            new
                            {
                                i.Action,
                                review.Dispositions,
                                groups = selected.Select(g => new
                                {
                                    g.Id,
                                    g.State,
                                    g.Regions,
                                }),
                                created = added.Select(g => new
                                {
                                    g.Id,
                                    g.Regions,
                                    g.Parents,
                                }),
                            },
                            i.Reason
                        );
                        return Api.View(report);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/pages",
            async (Guid id, Guid document, PageCommand i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var report = await svc.Version(id, i.Version, c.User);
                        svc.Edit(report, c.User, i.Reason);
                        Guard.Reason(i.Reason);
                        Guard.Require(
                            report.Attachments.Any(a => a.Id == document && !a.Archived),
                            "source",
                            "Document sursă indisponibil.",
                            "Source unavailable."
                        );
                        var review = await db.DocumentReviews.SingleAsync(r => r.Id == document && r.ReportId == id);
                        Guard.Require(
                            i.Page >= 1 && i.Page <= review.PageCount,
                            "page",
                            "Pagină invalidă.",
                            "Invalid page."
                        );
                        var before = review.Dispositions;
                        var pages = Serialization.Read<List<PageDisposition>>(before);
                        pages.RemoveAll(p => p.Page == i.Page
                            || (p.Kind == "CoverageConfirmed"
                                && (p.CoveragePage ?? p.Page) == i.Page));
                        if (i.Supporting)
                            pages.Add(new(i.Page, i.Reason));
                        review.Dispositions = Serialization.Write(pages);
                        Reports.Touch(report);
                        svc.Log(
                            c.User,
                            id,
                            document.ToString(),
                            "PageDispositionChanged",
                            before,
                            review.Dispositions,
                            i.Reason
                        );
                        return Api.View(report);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/page-coverage",
            async (Guid id, Guid document, PageCoverageCommand i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var report = await svc.Version(id, i.Version, c.User);
                        svc.Edit(report, c.User, i.Reason);
                        Guard.Reason(i.Reason);
                        var source = report.Attachments.SingleOrDefault(a => a.Id == document && !a.Archived);
                        Guard.Require(source != null, "source", "Document inexistent.", "Source unavailable.", 404);
                        var review = await db.DocumentReviews.SingleAsync(r => r.Id == document && r.ReportId == id);
                        var groups = await db.DocumentGroups.Where(g => g.BundleId == document).ToListAsync();
                        Guard.Require(
                            NeedsPageCoverage(source!, groups),
                            "coverage_mode",
                            "Confirmarea completitudinii este necesară doar pentru documente cu mai multe bonuri.",
                            "Receipt completeness confirmation applies only to multi-receipt documents."
                        );
                        Guard.Require(
                            i.Page >= 1 && i.Page <= review.PageCount,
                            "page", "Pagină invalidă.", "Invalid page."
                        );
                        Guard.Require(
                            !i.Complete || !Unassigned(review, groups).Contains(i.Page),
                            "coverage_unassigned",
                            "Atribuie toate paginile înainte de confirmare.",
                            "Assign the page before confirming completeness."
                        );
                        var before = review.Dispositions;
                        var dispositions = Serialization.Read<List<PageDisposition>>(before);
                        dispositions.RemoveAll(x => x.Kind == "CoverageConfirmed"
                            && (x.CoveragePage ?? x.Page) == i.Page);
                        if (i.Complete)
                            dispositions.Add(new(0, i.Reason, "CoverageConfirmed", i.Page));
                        review.Dispositions = Serialization.Write(dispositions);
                        Reports.Touch(report);
                        svc.Log(c.User, id, document.ToString(),
                            "PageCoverageReviewed", before, review.Dispositions, i.Reason);
                        return Api.View(report);
                    }
                )
        );
        api.MapGet(
            "/reports/{id:guid}/documents/{document:guid}/feedback",
            async (Guid id, Guid document, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                Guard.Require(
                    report.Attachments.Any(a => a.Id == document),
                    "missing",
                    "Document inexistent.",
                    "Document not found.",
                    404
                );
                return await Feedback(db, id, document);
            }
        );
        api.MapGet(
            "/reports/{id:guid}/documents/{document:guid}/extractions",
            async (Guid id, Guid document, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                Guard.Require(
                    report.Attachments.Any(a => a.Id == document),
                    "missing",
                    "Document inexistent.",
                    "Document not found.",
                    404
                );
                return await db
                    .ExtractionAttempts.Where(a => a.BundleId == document)
                    .OrderBy(a => a.Created)
                    .Select(a => new
                    {
                        a.Id,
                        a.Created,
                        a.Fingerprint,
                        a.Json,
                    })
                    .ToListAsync();
            }
        );
    }
}
