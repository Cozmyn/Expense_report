using Microsoft.EntityFrameworkCore;

namespace Decont;

/// <summary>
/// Privacy-preserving supplier learning from explicitly selected and then verified source regions.
/// No model training, document export, new DB tables, or automatic financial confirmation.
/// </summary>
public static class SupplierOcrLearning
{
    public record Example(string Cui, Guid Bundle, string Field, SourceBox Box, string? LayoutId = null);

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
                if (fields.ContainsKey(group.Key))
                    continue; // Never silently override an existing/ambiguous OCR result.
                var page = result.Pages.FirstOrDefault(p =>
                    candidate.Pages.Contains(p.Number) && p.Words != null && p.Words.Count > 0);
                if (page == null) continue;
                var regions = candidate.Regions ?? candidate.Pages.Select(n => new SourceRegion(n)).ToList();
                if (result.Candidates.Count(other => other != candidate
                        && other.Pages.Contains(page.Number)) > 0
                    && regions.Any(r => r.Page == page.Number && r.X == 0
                        && r.Y == 0 && r.Width == 1 && r.Height == 1))
                    continue;

                // Keep independently managed versions separate. Before a supplier
                // is curated, cluster verified geometry instead of rejecting all
                // templates when the supplier changes its till or branch layout.
                var layouts = new List<List<Example>>();
                foreach (var version in group.GroupBy(x => x.LayoutId ?? "implicit"))
                {
                    var samples = version.DistinctBy(x => x.Bundle).ToList();
                    if (version.Key != "implicit")
                    {
                        layouts.Add(samples);
                        continue;
                    }
                    foreach (var seed in samples)
                    {
                        var matching = samples.Where(x => Similar(x.Box, seed.Box)).ToList();
                        if (matching.Count < 2) continue;
                        var key = string.Join(",", matching.Select(x => x.Bundle).Order());
                        if (!layouts.Any(l => string.Join(",", l.Select(x => x.Bundle).Order()) == key))
                            layouts.Add(matching);
                    }
                }
                var proposals = new List<(string Value, OcrField Field, int Support)>();
                foreach (var samples in layouts)
                {
                    if (samples.Count < 2 || !samples.All(x => Similar(x.Box, samples[0].Box)))
                        continue;
                    var template = samples[0].Box;
                    var words = page.Words!
                        .Where(w =>
                        {
                            var x = w.Box.X + w.Box.Width / 2;
                            var y = w.Box.Y + w.Box.Height / 2;
                            return regions.Any(r => r.Page == page.Number
                                    && x >= r.X && x <= r.X + r.Width
                                    && y >= r.Y && y <= r.Y + r.Height)
                                && x >= Math.Max(0, template.X - .02m)
                                && x <= Math.Min(1, template.X + template.Width + .02m)
                                && y >= Math.Max(0, template.Y - .02m)
                                && y <= Math.Min(1, template.Y + template.Height + .02m);
                        }).OrderBy(w => w.Box.Y).ThenBy(w => w.Box.X).ToList();
                    if (words.Count == 0) continue;
                    var evidence = string.Join(" ", words.Select(w => w.Text));
                    var suggestion = OcrFieldSelection.Extract(group.Key, evidence);
                    if (suggestion == null) continue;
                    var left = words.Min(w => w.Box.X);
                    var top = words.Min(w => w.Box.Y);
                    var actualBox = new SourceBox(
                        left, top,
                        words.Max(w => w.Box.X + w.Box.Width) - left,
                        words.Max(w => w.Box.Y + w.Box.Height) - top);
                    proposals.Add((suggestion, new OcrField(
                        suggestion, page.Number, evidence, "ReviewRequired", null,
                        actualBox, evidence, "SupplierTemplate"), samples.Count));
                }
                if (proposals.Select(p => p.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                {
                    warnings.Add("SUPPLIER_LAYOUT_AMBIGUOUS: Șabloanele furnizorului sugerează valori diferite. Confirmă manual pe original. / Supplier layouts suggest different values. Review the original.");
                    continue;
                }
                if (proposals.Count == 0) continue;
                fields[group.Key] = proposals.OrderByDescending(p => p.Support).First().Field;
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
        var managed = SupplierOcrTemplates.Latest(await SupplierOcrTemplates.Versions(db))
            .Where(v => detected.Contains(v.Cui)).ToList();
        var curatedVendors = managed.Select(v => v.Cui).ToHashSet(StringComparer.Ordinal);
        List<Example> examples = managed.Where(v => v.Enabled)
            .SelectMany(v => v.Examples.Select(e => e with { LayoutId = v.LayoutId }))
            .ToList();
        // Once a supplier's layouts are managed, disabled versions cannot
        // reappear through implicit learning from the same historical receipts.
        var implicitVendors = detected.Except(curatedVendors).ToHashSet(StringComparer.Ordinal);
        if (implicitVendors.Count == 0)
            return Suggest(result, examples);
        var verified = db.DocumentGroups
            .AsNoTracking()
            .Where(g => g.State == "Verified" && g.CorrectedExpense != null)
            .OrderByDescending(g => g.VerifiedAt)
            .Select(g => new { g.BundleId, g.Fields, g.CorrectedExpense })
            .AsAsyncEnumerable();
        var perVendorCounts = implicitVendors.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        await foreach (var item in verified)
        {
            try
            {
                var expense = Serialization.Read<ExpenseData>(item.CorrectedExpense!);
                var cui = Cui(expense.Cui);
                if (!implicitVendors.Contains(cui) || perVendorCounts[cui] >= 300)
                    continue;
                perVendorCounts[cui]++;
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
            if (perVendorCounts.Values.All(n => n >= 300))
                break;
        }
        return Suggest(result, examples);
    }
}
