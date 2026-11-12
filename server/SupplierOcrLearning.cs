using Microsoft.EntityFrameworkCore;

namespace Decont;

/// <summary>
/// Privacy-preserving supplier learning from explicitly selected and then verified source regions.
/// No model training, document export, new DB tables, or automatic financial confirmation.
/// </summary>
public static class SupplierOcrLearning
{
    public record Example(string Cui, Guid Bundle, string Field, SourceBox Box);

    private static string Cui(string? value) => Rules.Cui(value);

    public static bool MatchesVerifiedValue(ExpenseData expense, string key, OcrField field)
    {
        if (string.IsNullOrWhiteSpace(field.Value))
            return false;
        if (key == "amount")
            return ReceiptParser.Number(field.Value) == expense.Amount;
        var final = key switch
        {
            "supplier" => expense.Supplier,
            "cui" => expense.Cui,
            "invoice" => expense.Invoice,
            "date" => expense.Date,
            "currency" => expense.Currency,
            "description" => expense.Description,
            _ => null,
        };
        return final != null &&
            (key == "cui"
                ? Cui(final) == Cui(field.Value)
                : string.Equals(final.Trim(), field.Value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool Similar(SourceBox a, SourceBox b) =>
        Math.Abs(a.X - b.X) <= .08m
        && Math.Abs(a.Y - b.Y) <= .08m
        && Math.Abs(a.Width - b.Width) <= .12m
        && Math.Abs(a.Height - b.Height) <= .12m;

    public static OcrResult Suggest(OcrResult result, IEnumerable<Example> examples)
    {
        var history = examples
            .Where(e => OcrFieldSelection.Fields.Contains(e.Field) && e.Box.Width > 0 && e.Box.Height > 0)
            .ToList();
        if (history.Count == 0)
            return result;
        var candidates = result.Candidates.Select(candidate =>
        {
            var cui = Cui(candidate.Fields.GetValueOrDefault("cui")?.Value);
            if (cui.Length == 0 || !Rules.ValidCui(cui))
                return candidate;
            var matched = history.Where(e => Cui(e.Cui) == cui).ToList();
            if (matched.Count == 0)
                return candidate;
            var fields = new Dictionary<string, OcrField>(candidate.Fields);
            var warnings = new List<string>(candidate.Warnings);
            foreach (var group in matched.GroupBy(x => x.Field))
            {
                if (fields.GetValueOrDefault(group.Key)?.Value != null)
                    continue; // Parsed values always remain available; templates never override them.
                // Two separate verified documents, with consistent region geometry, are required.
                var samples = group.DistinctBy(x => x.Bundle).ToList();
                if (samples.Count < 2)
                    continue;
                var sample = samples[0].Box;
                if (!samples.Skip(1).Any(x => Similar(x.Box, sample)))
                    continue;
                // Reject mixed supplier layouts; those need explicit selection and verification.
                if (!samples.All(x => Similar(x.Box, sample)))
                    continue;
                var page = result.Pages.FirstOrDefault(p =>
                    candidate.Pages.Contains(p.Number) && p.Words != null && p.Words.Count > 0
                );
                if (page == null)
                    continue;
                // A supplier may have two receipts on the same scanned page.
                // Only the candidate's own source region can contribute a hint.
                var regions = candidate.Regions ?? candidate.Pages.Select(n => new SourceRegion(n)).ToList();
                if (result.Candidates.Count(other => other != candidate
                        && other.Pages.Contains(page.Number)) > 0
                    && regions.Any(r => r.Page == page.Number && r.X == 0
                        && r.Y == 0 && r.Width == 1 && r.Height == 1))
                    continue; // Shared-page, unbounded source is ambiguous.
                var words = page.Words!
                    .Where(w =>
                    {
                        var x = w.Box.X + w.Box.Width / 2;
                        var y = w.Box.Y + w.Box.Height / 2;
                        return regions.Any(r => r.Page == page.Number
                                && x >= r.X && x <= r.X + r.Width
                                && y >= r.Y && y <= r.Y + r.Height)
                            && x >= Math.Max(0, sample.X - .02m)
                            && x <= Math.Min(1, sample.X + sample.Width + .02m)
                            && y >= Math.Max(0, sample.Y - .02m)
                            && y <= Math.Min(1, sample.Y + sample.Height + .02m);
                    })
                    .OrderBy(w => w.Box.Y).ThenBy(w => w.Box.X)
                    .ToList();
                var evidence = string.Join(" ", words.Select(w => w.Text));
                var suggestion = OcrFieldSelection.Extract(group.Key, evidence);
                if (suggestion == null)
                    continue;
                fields[group.Key] = new OcrField(
                    suggestion, page.Number, evidence, "ReviewRequired", null,
                    sample, evidence, "SupplierTemplate"
                );
                warnings.Add(
                    "SUPPLIER_HINT: Propunere din bonuri confirmate ale aceluiași furnizor. Verifică originalul; nu reprezintă o confirmare. / Suggested from verified receipts of the same supplier. Check the original; this is not confirmed."
                );
            }
            return candidate with { Fields = fields, Warnings = warnings };
        }).ToList();
        return result with { Candidates = candidates };
    }

    public static async Task<OcrResult> Suggest(Db db, OcrResult result)
    {
        if (!result.Candidates.Any(c => !string.IsNullOrWhiteSpace(c.Fields.GetValueOrDefault("cui")?.Value)))
            return result;
        // Select verified history by vendor before bounding any result set.
        // A global Take(300) silently forgets rare suppliers. The persisted
        // corrected expense is JSON, so on legacy schemas the fiscal identifier
        // cannot be indexed here: query vendor-specific batches in bounded pages
        // instead of imposing a global 300-row cutoff. A normalized indexed
        // supplier identity is still recommended for high-volume installations.
        var detected = result.Candidates
            .Select(c => Cui(c.Fields.GetValueOrDefault("cui")?.Value))
            .Where(c => c.Length > 0 && Rules.ValidCui(c))
            .ToHashSet(StringComparer.Ordinal);
        if (detected.Count == 0)
            return result;
        var verified = await db.DocumentGroups
            .AsNoTracking()
            .Where(g => g.State == "Verified" && g.CorrectedExpense != null)
            .OrderByDescending(g => g.VerifiedAt)
            .Select(g => new { g.BundleId, g.Fields, g.CorrectedExpense })
            .ToListAsync();
        List<Example> examples = [];
        foreach (var item in verified)
        {
            try
            {
                var expense = Serialization.Read<ExpenseData>(item.CorrectedExpense!);
                var cui = Cui(expense.Cui);
                if (!detected.Contains(cui))
                    continue;
                var fields = Serialization.Read<Dictionary<string, OcrField>>(item.Fields);
                foreach (var (key, field) in fields)
                    if (field.Source == "HumanSelected" && field.Box != null
                        && MatchesVerifiedValue(expense, key, field))
                        examples.Add(new(cui, item.BundleId, key, field.Box));
            }
            catch (Exception)
            {
                // Invalid legacy rows are ignored, never used to train.
            }
        }
        return Suggest(result, examples);
    }
}
