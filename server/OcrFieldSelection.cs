using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record OcrSourceSelection(long Version, string Field, SourceRegion Region, string? Reason = null);

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
            var numbers = Regex.Matches(
                text, @"(?<!\d)[-+]?\d+(?:[ .,]\d{3})*[.,]\d{2}(?!\d)"
            );
            var last = numbers.Cast<System.Text.RegularExpressions.Match>().LastOrDefault();
            var value = last == null
                ? Regex.IsMatch(text, @"\A[-+]?\d+\z")
                    ? ReceiptParser.Number(text)
                    : null
                : ReceiptParser.Number(last.Value);
            return value?.ToString("0.00", CultureInfo.InvariantCulture);
        }
        if (field == "date")
        {
            var match = Regex.Match(text, @"\b(\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})\b");
            return match.Success
                && DateOnly.TryParseExact(
                    match.Value,
                    ["dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var value
                ) ? value.ToString("yyyy-MM-dd") : null;
        }
        if (field == "currency")
        {
            var match = Regex.Match(text, @"\b(RON|LEI|EUR|USD|GBP|CHF|HUF|PLN|CZK|BGN|DKK|NOK|SEK)\b", RegexOptions.IgnoreCase);
            return match.Success ? (match.Value.Equals("LEI", StringComparison.OrdinalIgnoreCase)
                ? "RON" : match.Value.ToUpperInvariant()) : null;
        }
        if (field == "cui")
        {
            var match = Regex.Match(text, @"(?:RO\s*)?(\d{2,10})\b", RegexOptions.IgnoreCase);
            var cui = match.Success ? Rules.Cui(match.Groups[1].Value) : "";
            return Rules.ValidCui(cui) ? cui : null;
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
            async (Guid id, Guid document, Guid group, OcrSourceSelection input, Db db, Reports svc, HttpContext context) =>
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
                        var result = Serialization.Read<OcrResult>(review.OriginalExtraction);
                        var raw = DocumentAssistance.Evidence(result, [input.Region]);
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
                            raw, "HumanSelected"
                        );
                        item.Fields = Serialization.Write(fields);
                        Reports.Touch(report);
                        DocumentEvents.Emit(
                            db, id, document, "OcrSourceSelected",
                            Reports.Actor(context.User),
                            group + "|" + report.Version + "|" + input.Field,
                            new { group, input.Field, input.Region, financeCorrection }
                        );
                        svc.Log(
                            context.User, id, document.ToString(), "OcrSourceSelected", before,
                            new { item.Id, input.Field, input.Region, financeCorrection }, input.Reason
                        );
                        return Api.View(report);
                    }
                )
        );
    }
}
