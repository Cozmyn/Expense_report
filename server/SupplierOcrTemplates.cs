using Microsoft.EntityFrameworkCore;

namespace Decont;

/// <summary>
/// Versioned, per-layout supplier OCR configurations in the existing Master
/// ledger. Each revision is append-only: deactivating one layout never erases
/// its source documents, verified boxes or previous versions.
/// No OCR suggestion is authoritative for accounting.
/// </summary>
public static class SupplierOcrTemplates
{
    public const string Kind = "supplierOcrTemplates";
    public record Version(string Cui, string LayoutId, string Name, int Revision,
        bool Enabled, List<SupplierOcrLearning.Example> Examples);
    public record Create(string Cui, string LayoutId, string Name,
        Guid[] VerifiedGroups, string Reason);
    public record Switch(string Reason);

    public static bool ValidLayout(string text) =>
        text.Length is >= 2 and <= 40
        && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static bool ValidBox(SourceBox b) =>
        b.X >= 0 && b.Y >= 0 && b.Width > 0 && b.Height > 0
        && b.X + b.Width <= 1 && b.Y + b.Height <= 1;

    public static bool Similar(SourceBox a, SourceBox b) =>
        Math.Abs(a.X - b.X) <= .08m && Math.Abs(a.Y - b.Y) <= .08m
        && Math.Abs(a.Width - b.Width) <= .12m && Math.Abs(a.Height - b.Height) <= .12m;

    public static IEnumerable<Version> Latest(IEnumerable<Version> versions) =>
        versions.GroupBy(x => (x.Cui, x.LayoutId))
            .Select(g => g.OrderByDescending(x => x.Revision).First())
            .OrderBy(x => x.Cui).ThenBy(x => x.LayoutId);

    public static async Task<List<Version>> Versions(Db db, string? cui = null)
    {
        var normalized = string.IsNullOrWhiteSpace(cui) ? null : Rules.Cui(cui);
        var entries = await db.Masters.AsNoTracking()
            .Where(m => m.Kind == Kind && (normalized == null || m.Code.StartsWith(normalized + ":")))
            .ToListAsync();
        // Fail closed for corrupt historic metadata, rather than suggesting
        // from an untrusted layout. Normal writes are schema-validated below.
        return entries.Select(x => Serialization.Read<Version>(x.Json)).ToList();
    }

    public static async Task<List<SupplierOcrLearning.Example>> VerifiedExamples(
        Db db, string cui, int maxDocuments = 100)
    {
        List<SupplierOcrLearning.Example> examples = [];
        var verifiedDocs = new HashSet<Guid>();
        var rows = db.DocumentGroups.AsNoTracking()
            .Where(g => g.State == "Verified" && g.CorrectedExpense != null)
            .OrderByDescending(g => g.VerifiedAt)
            .Select(g => new { g.BundleId, g.Fields, g.CorrectedExpense })
            .AsAsyncEnumerable();
        await foreach (var group in rows)
        {
            try
            {
                var expense = Serialization.Read<ExpenseData>(group.CorrectedExpense!);
                if (Rules.Cui(expense.Cui) != cui) continue;
                if (verifiedDocs.Add(group.BundleId) && verifiedDocs.Count > maxDocuments)
                    break;
                var fields = Serialization.Read<Dictionary<string, OcrField>>(group.Fields);
                foreach (var (fieldName, field) in fields)
                    if (OcrFieldSelection.Fields.Contains(fieldName)
                        && field.Source == "HumanSelected" && field.Box is SourceBox box
                        && ValidBox(box) && SupplierOcrLearning.MatchesVerifiedValue(expense, fieldName, field))
                        examples.Add(new(cui, group.BundleId, fieldName, box));
            }
            catch (Exception)
            {
                // Malformed legacy training data is ignored, never surfaced as
                // an editable financial value or trusted template.
            }
        }
        return examples;
    }

    public static List<SupplierOcrLearning.Example> Build(string cui, string layoutId,
        IEnumerable<SupplierOcrLearning.Example> verified, IEnumerable<Guid> requested)
    {
        var approved = requested.Distinct().ToHashSet();
        var selected = verified.Where(e => approved.Contains(e.Bundle)).ToList();
        var accepted = new List<SupplierOcrLearning.Example>();
        foreach (var group in selected.GroupBy(e => e.Field))
        {
            var sources = group.DistinctBy(e => e.Bundle).ToList();
            if (sources.Count < 2 || sources.Count != approved.Count) continue;
            if (!sources.All(s => Similar(s.Box, sources[0].Box))) continue;
            accepted.AddRange(sources.Select(e => e with { LayoutId = layoutId }));
        }
        return accepted;
    }

    private static async Task<Version?> Current(Db db, string cui, string layoutId) =>
        Latest(await Versions(db, cui)).FirstOrDefault(x => x.LayoutId == layoutId);

    private static Master Entry(Version version) => new()
    {
        Kind = Kind,
        Code = version.Cui + ":" + version.LayoutId + ":" + version.Revision.ToString("D6"),
        NameRo = version.Name,
        NameEn = version.Name,
        From = DateOnly.FromDateTime(DateTime.UtcNow),
        Active = version.Enabled,
        Json = Serialization.Write(version),
    };

    public static void Map(RouteGroupBuilder api)
    {
        var route = api.MapGroup("/finance/supplier-ocr-templates")
            .RequireAuthorization(p => p.RequireRole("Finance", "Admin"));
        route.MapGet("", async (Db db, string? cui) =>
            Latest(await Versions(db, cui)).ToList());

        route.MapGet("/examples", async (Db db, string cui) =>
        {
            var key = Rules.Cui(cui);
            Guard.Require(Rules.ValidCui(key), "cui", "CUI invalid.", "Invalid supplier VAT ID.");
            var samples = await VerifiedExamples(db, key);
            return samples.GroupBy(x => x.Bundle).Select(group => new
            {
                bundle = group.Key,
                fields = group.Select(e => e.Field).Order().ToArray(),
            }).ToList();
        });

        route.MapPost("", async (Create input, Db db, Reports svc, HttpContext c) =>
            await Api.Atomic(db, c, input, async () =>
            {
                Guard.Reason(input.Reason);
                var cui = Rules.Cui(input.Cui);
                Guard.Require(Rules.ValidCui(cui) && ValidLayout(input.LayoutId)
                    && input.Name.Trim().Length is >= 2 and <= 100
                    && input.VerifiedGroups.Distinct().Count() is >= 2 and <= 12
                    && input.VerifiedGroups.Length == input.VerifiedGroups.Distinct().Count(),
                    "template", "CUI, nume, șablon sau documente insuficiente.",
                    "Invalid supplier/layout/name or too few distinct verified documents.");
                var trusted = await VerifiedExamples(db, cui, 1000);
                var examples = Build(cui, input.LayoutId, trusted, input.VerifiedGroups);
                Guard.Require(examples.Count >= 2, "template_samples",
                    "Selectează cel puțin două documente diferite cu aceleași câmpuri verificate manual.",
                    "Select two separate documents with matching human-verified fields.", 409);
                var current = await Current(db, cui, input.LayoutId);
                if (current == null)
                    Guard.Require(Latest(await Versions(db, cui)).Count() < 16,
                        "template_limit", "Prea multe șabloane pentru furnizor.",
                        "Too many supplier layouts.", 409);
                var next = new Version(cui, input.LayoutId, input.Name.Trim(),
                    (current?.Revision ?? 0) + 1, true, examples);
                var entry = Entry(next);
                db.Masters.Add(entry);
                svc.Log(c.User, null, entry.Id.ToString(),
                    "SupplierOcrTemplateVersioned", after: next, reason: input.Reason);
                return next;
            }));

        route.MapPost("/{cui}/{layoutId}/toggle", async (
            string cui, string layoutId, bool enabled, Switch input, Db db, Reports svc, HttpContext c) =>
            await Api.Atomic(db, c, input, async () =>
            {
                Guard.Reason(input.Reason);
                var key = Rules.Cui(cui);
                Guard.Require(Rules.ValidCui(key) && ValidLayout(layoutId),
                    "template", "Identificator de șablon invalid.", "Invalid template identifier.");
                var current = await Current(db, key, layoutId)
                    ?? throw new DomainException("template_missing",
                        "Șablonul nu există.", "Template not found.", 404);
                Guard.Require(current.Enabled != enabled, "template_state",
                    "Șablonul are deja această stare.", "Template already has the requested state.", 409);
                var next = current with { Revision = current.Revision + 1, Enabled = enabled };
                var entry = Entry(next);
                db.Masters.Add(entry);
                svc.Log(c.User, null, entry.Id.ToString(), enabled
                    ? "SupplierOcrTemplateReactivated" : "SupplierOcrTemplateDisabled",
                    before: current, after: next, reason: input.Reason);
                return next;
            }));
    }
}
