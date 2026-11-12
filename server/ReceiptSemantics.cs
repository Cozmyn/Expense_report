using System.Globalization;
using System.Text.RegularExpressions;

namespace Decont;

/// <summary>
/// Conservative document-field disambiguation: if the source text is not
/// sufficient to identify one financial value, no value is guessed.
/// Every proposed alternative retains its own evidence text.
/// </summary>
public static class ReceiptSemantics
{
    public record Choice(
        string? Value,
        string Evidence,
        bool Ambiguous = false,
        List<(string Value, string Evidence)>? Alternatives = null
    );

    static readonly Regex DatePattern = new(
        @"\b(?:\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})\b",
        RegexOptions.Compiled
    );
    static readonly string[] DateFormats = ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"];
    static readonly Regex DueLabel = new(
        @"(?i)\b(?:SCADEN(?:T[ĂA])?|DUE\s*DATE|PAYMENT\s*DUE|TERMEN(?:UL)?\s+(?:DE\s+)?PLAT[ĂA])\b",
        RegexOptions.Compiled
    );
    static readonly Regex IssueLabel = new(
        @"(?i)\b(?:EMIS[ĂA]|EMITERII|ISSUED|ISSUE\s*DATE|DATE\s+OF\s+ISSUE|INVOICE\s+DATE|DATA\s+FACTURII|DATA\s+EMITERII)\b",
        RegexOptions.Compiled
    );
    static readonly Regex TotalLabel = new(
        @"(?i)\b(?:TOTAL|GRAND\s+TOTAL|AMOUNT\s+DUE|SUMA(?:\s+TOTAL[ĂA])?\s+(?:DE\s+)?(?:PLAT[ĂA]|ACHITAT[ĂA])|VALOARE(?:A)?\s+TOTAL[ĂA])\b",
        RegexOptions.Compiled
    );
    static readonly Regex ExcludeTotal = new(
        @"(?i)\b(?:SUB[-\s]?TOTAL|TOTAL\s*(?:TVA|VAT|TAX|BAZ[ĂA]|DISCOUNT|REDUCER)|REST(?:UL)?|CHANGE|AVANS|SOLD\s+ANTERIOR|PREVIOUS\s+BALANCE)\b",
        RegexOptions.Compiled
    );
    static readonly Regex MoneyPattern = new(
        @"(?<![\d])[-+]?\d+(?:[ .,\u00a0\u202f]\d{3})*[.,]\d{2}(?!\d)",
        RegexOptions.Compiled
    );

    public static Choice IssueDate(IReadOnlyList<string> lines)
    {
        List<(string Value, string Evidence, bool Issue, bool AmbiguousOrder)> found = [];
        foreach (var line in lines)
        {
            if (DueLabel.IsMatch(line))
                continue;
            foreach (System.Text.RegularExpressions.Match match in DatePattern.Matches(line))
            {
                if (!DateOnly.TryParseExact(match.Value, DateFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                    continue;
                var parts = match.Value.Split('/');
                var ambiguousOrder = parts.Length == 3 &&
                    int.TryParse(parts[0], out var day) && int.TryParse(parts[1], out var month) &&
                    day <= 12 && month <= 12 && day != month;
                found.Add((date.ToString("yyyy-MM-dd"), line, IssueLabel.IsMatch(line), ambiguousOrder));
            }
        }
        var preferred = found.Any(c => c.Issue) ? found.Where(c => c.Issue).ToList() : found;
        if (preferred.Count == 0)
            return new(null, "");
        var alternatives = preferred.Select(c => (c.Value, c.Evidence)).Distinct().ToList();
        if (preferred.Any(c => c.AmbiguousOrder) || preferred.Select(c => c.Value).Distinct().Count() > 1)
            return new(null, string.Join(" | ", preferred.Select(c => c.Evidence).Distinct().Take(4)),
                true, alternatives);
        return new(preferred[0].Value, preferred[0].Evidence);
    }

    public static Choice GrossTotal(IReadOnlyList<string> lines)
    {
        List<(string Value, string Evidence)> alternatives = [];
        var ambiguousLine = false;
        foreach (var line in lines.Where(l => TotalLabel.IsMatch(l) && !ExcludeTotal.IsMatch(l)))
        {
            // Equivalents are not a second charge; use the primary portion
            // only if it unambiguously contains one monetary value.
            var equivalent = Regex.Match(line, @"(?i)\b(?:ECHIVALENT|EQUIVALENT)\b");
            var primary = equivalent.Success ? line[..equivalent.Index] : line;
            var values = MoneyPattern.Matches(primary)
                .Select(m => ReceiptParser.Number(m.Value))
                .Where(n => n != null)
                .Select(n => n!.Value.ToString("0.00", CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
            if (values.Count > 1)
                ambiguousLine = true;
            foreach (var value in values)
                alternatives.Add((value, line));
        }
        if (alternatives.Count == 0)
            return new(null, "");
        var distinct = alternatives.Select(x => x.Value).Distinct().ToList();
        if (ambiguousLine || distinct.Count > 1)
            return new(null, string.Join(" | ", alternatives.Select(c => c.Evidence).Distinct().Take(4)),
                true, alternatives.Distinct().ToList());
        return new(distinct[0], alternatives[0].Evidence);
    }
}
