using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record OcrSourceSelection(long Version, string Field, SourceRegion Region, string? Reason = null, Guid? AttemptId = null, bool Reread = false);

/// <summary>
/// A source annotation records where a user selected text. It never verifies a receipt or
/// approves an amount: all existing accounting validation still applies on final save.
/// </summary>
public static class OcrFieldSelection
{
    public static readonly HashSet<string> Fields =
    [
        "supplier", "cui", "invoice", "date", "currency", "amount", "description"
    ];

    public static bool Supported(string field) =>
        Fields.Contains(field) || Regex.IsMatch(field, @"\Atax(?:Base|Vat):\d{1,2}(?:\.\d+)?\z");

    public static string? Extract(string field, string evidence)
    {
        var text = evidence.Trim();
        if (text.Length is 0 or > 1000 || !Supported(field))
            return null;
        if (field == "amount" || field.StartsWith("taxBase:") || field.StartsWith("taxVat:"))
        {
            var matches = Regex.Matches(text, @"(?<!\d)[-+]?\d+(?:[ .,\u00a0\u202f]\d{3})*[.,]\d{2}(?!\d)")
                .Select(m => ReceiptParser.Number(m.Value))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            var value = matches.Count == 0 && Regex.IsMatch(text, @"\A[-+]?\d+\z")
                ? ReceiptParser.Number(text)
                : null;
            if (matches.Count == 1)
                value = matches[0];
            if (field == "amount")
            {
                // A highlighted area can include both the original amount and a
                // converted equivalent. Never silently pick the last number.
                var equivalent = Regex.Match(text, @"(?i)\b(?:ECHIVALENT|EQUIVALENT)\b");
                if (equivalent.Success && Regex.IsMatch(text, @"(?i)\bTOTAL\b"))
                {
                    var primary = Regex.Matches(text[..equivalent.Index],
                            @"(?<!\d)[-+]?\d+(?:[ .,\u00a0\u202f]\d{3})*[.,]\d{2}(?!\d)")
                        .Select(m => ReceiptParser.Number(m.Value))
                        .Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();
                    value = primary.Count == 1 ? primary[0] : null;
                }
                else if (matches.Distinct().Count() > 1)
                    value = null;
            }
            else if (matches.Count > 1)
            {
                // Tax base and paid VAT may share one printed line only when
                // the VAT rate and exact two monetary amounts are explicit.
                var rate = field.Split(':')[1];
                var printed = Regex.Matches(text, @"(?<!\d)(\d{1,2}(?:[.,]\d+)?)\s*%")
                    .Select(m => m.Groups[1].Value.Replace(',', '.')).Distinct().ToList();
                if (matches.Count == 2 && printed.Count == 1
                    && decimal.TryParse(printed[0], NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var percentage)
                    && decimal.TryParse(rate, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var expectedRate)
                    && percentage == expectedRate
                    && Regex.IsMatch(text, @"(?i)\b(?:TVA|VAT|TAX)\b"))
                {
                    // Printed columns may be base/VAT or VAT/base. Determine
                    // direction only when the arithmetic supports exactly one.
                    // Never assume that the first number is a base.
                    var firstIsBase = Math.Abs(matches[0] * expectedRate / 100m - matches[1]) <= .05m;
                    var secondIsBase = Math.Abs(matches[1] * expectedRate / 100m - matches[0]) <= .05m;
                    if (firstIsBase == secondIsBase)
                        value = null;
                    else
                    {
                        var taxableBase = firstIsBase ? matches[0] : matches[1];
                        var paidVat = firstIsBase ? matches[1] : matches[0];
                        value = field.StartsWith("taxBase:") ? taxableBase : paidVat;
                    }
                }
                else
                    value = null;
            }
            return value?.ToString("0.00", CultureInfo.InvariantCulture);
        }
        if (field == "date")
        {
            var parsed = new List<DateOnly>();
            foreach (System.Text.RegularExpressions.Match match in Regex.Matches(text,
                @"\b(?:\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})\b"))
            {
                var parts = match.Value.Split('/');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out var first) &&
                    int.TryParse(parts[1], out var second) &&
                    first <= 12 && second <= 12 && first != second)
                    return null; // Slash format could mean either day/month order.
                if (DateOnly.TryParseExact(match.Value,
                    ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    parsed.Add(date);
            }
            return parsed.Distinct().Count() == 1
                ? parsed[0].ToString("yyyy-MM-dd") : null;
        }
        if (field == "currency")
        {
            var primary = text;
            var equivalent = Regex.Match(text, @"(?i)\b(?:ECHIVALENT|EQUIVALENT)\b");
            if (equivalent.Success && Regex.IsMatch(text, @"(?i)\bTOTAL\b"))
                primary = text[..equivalent.Index];
            var codes = Regex.Matches(primary,
                    @"(?i)\b(?:RON|LEI|EUR|USD|GBP|CHF|HUF|PLN|CZK|BGN|DKK|NOK|SEK)\b")
                .Select(m => m.Value.Equals("LEI", StringComparison.OrdinalIgnoreCase)
                    ? "RON" : m.Value.ToUpperInvariant())
                .Distinct().ToArray();
            return codes.Length == 1 ? codes[0] : null;
        }
        if (field == "cui")
        {
            var codes = Regex.Matches(text, @"(?:RO\s*)?(\d{2,10})\b", RegexOptions.IgnoreCase)
                .Select(m => Rules.Cui(m.Groups[1].Value))
                .Where(Rules.ValidCui).Distinct().ToArray();
            return codes.Length == 1 ? codes[0] : null;
        }
        if (field == "invoice")
            return Regex.Replace(text, @"(?i)^(?:FACTUR[ĂA]|INVOICE|BON(?:\s+FISCAL)?|DOCUMENT)\s*(?:NR\.?|NO\.?|NUMBER|#)?\s*[:#-]?\s*", "")
                .Split(' ', '\n', '\r').FirstOrDefault()?.Trim();
        // Supplier/description are user-selected free text, never a learned accounting rule.
        return text[..Math.Min(240, text.Length)];
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/groups/{group:guid}/select-field",
            async (Guid id, Guid document, Guid group, OcrSourceSelection input, Db db, Reports svc, IStore store, IConfiguration config, HttpContext context) =>
                await Api.Atomic(
                    db, context, input,
                    async () =>
                    {
                        var report = await svc.Version(id, input.Version, context.User);
                        svc.Edit(report, context.User, input.Reason);
                        Guard.Require(
                            Supported(input.Field),
                            "ocr_field",
                            "Selectează un câmp permis.",
                            "Select a supported field."
                        );
                        Guard.Require(
                            report.Attachments.Any(a => a.Id == document && !a.Archived && a.Scan == "Clean"),
                            "ocr_document",
                            "Documentul nu este disponibil pentru modificare.",
                            "Document is not available for changes."
                        );
                        var review = await db.DocumentReviews.SingleAsync(r => r.Id == document && r.ReportId == id);
                        var item = await db.DocumentGroups.SingleAsync(g => g.Id == group && g.BundleId == document);
                        var unverified = item.State == "ReviewRequired" && item.ExpenseId == null;
                        var financeCorrection = context.User.IsInRole("Finance")
                            && States.FinanceEdit(report.State)
                            && item.State == "Verified"
                            && item.ExpenseId != null
                            && report.Lines.Any(l => l.Id == item.ExpenseId && !l.Archived);
                        Guard.Require(
                            unverified || financeCorrection,
                            "ocr_group_state",
                            "Bonul verificat poate fi corectat în OCR doar de Finance în etapa de verificare.",
                            "Only Finance can correct verified OCR during financial review."
                        );
                        if (financeCorrection) Guard.Reason(input.Reason);
                        OcrReviews.ValidateRegions([input.Region], review.PageCount);
                        Guard.Require(
                            OcrReviews.Regions(item).Any(parent =>
                                input.Region.Page == parent.Page
                                && input.Region.X >= parent.X && input.Region.Y >= parent.Y
                                && input.Region.X + input.Region.Width <= parent.X + parent.Width
                                && input.Region.Y + input.Region.Height <= parent.Y + parent.Height
                            ),
                            "ocr_region",
                            "Selecția trebuie să fie în interiorul bonului.",
                            "Selection must be inside this receipt."
                        );
                        // Preserve the original extraction for audit, but select from the
                        // latest (or explicitly requested) extraction attempt.
                        var attempt = input.AttemptId is Guid requested
                            ? await db.ExtractionAttempts.SingleOrDefaultAsync(a => a.BundleId == document && a.Id == requested)
                            : await db.ExtractionAttempts.Where(a => a.BundleId == document)
                                .OrderByDescending(a => a.Created).FirstOrDefaultAsync();
                        Guard.Require(
                            input.AttemptId == null || attempt != null,
                            "ocr_attempt",
                            "Tentativa OCR selectată nu este disponibilă.",
                            "Selected OCR attempt is unavailable.",
                            409
                        );
                        var result = Serialization.Read<OcrResult>(attempt?.Json ?? review.OriginalExtraction);
                        var raw = input.Reread
                            ? await OcrRegionRecognition.Recognize(
                                report.Attachments.Single(a => a.Id == document), store, config,
                                input.Region, context.RequestAborted)
                            : DocumentAssistance.Evidence(result, [input.Region]);
                        var value = Extract(input.Field, raw);
                        Guard.Require(
                            value != null,
                            "ocr_source_unreadable",
                            "Textul selectat nu poate fi interpretat pentru acest câmp. Completează-l manual.",
                            "Selected text cannot be read as this field. Enter it manually."
                        );
                        var before = item.Fields;
                        var fields = Serialization.Read<Dictionary<string, OcrField>>(item.Fields);
                        fields[input.Field] = new OcrField(
                            value, input.Region.Page, raw,
                            financeCorrection ? "FinanceCorrected" : "ReviewRequired", null,
                            new SourceBox(input.Region.X, input.Region.Y, input.Region.Width, input.Region.Height),
                            raw, input.Reread ? "HumanReread" : "HumanSelected"
                        );
                        item.Fields = Serialization.Write(fields);
                        Reports.Touch(report);
                        DocumentEvents.Emit(
                            db, id, document, "OcrSourceSelected",
                            Reports.Actor(context.User),
                            group + "|" + report.Version + "|" + input.Field,
                            new { group, input.Field, input.Region, input.Reread, extractionAttemptId = attempt?.Id, financeCorrection }
                        );
                        svc.Log(
                            context.User, id, document.ToString(), "OcrSourceSelected", before,
                            new { item.Id, input.Field, input.Region, input.Reread, extractionAttemptId = attempt?.Id, financeCorrection }, input.Reason
                        );
                        return Api.View(report);
                    }
                )
        );
    }
}
