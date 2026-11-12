using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record OcrField(
    string? Value,
    int Page,
    string Evidence,
    string State = "ReviewRequired",
    decimal? Confidence = null,
    SourceBox? Box = null,
    string? RawValue = null,
    string Source = "Text"
);

public record OcrTax(
    decimal Rate,
    decimal Base,
    decimal Vat,
    int Page,
    string Evidence,
    SourceBox? Box = null,
    decimal? Confidence = null,
    string Source = "Printed"
);

public record OcrPage(
    int Number,
    string Text,
    string? Error = null,
    List<OcrWord>? Words = null,
    string Method = "Text",
    decimal? Width = null,
    decimal? Height = null,
    int Rotation = 0,
    SourceRegion? Region = null,
    int RecognitionRotation = 0,
    List<OrientationEvidence>? OrientationEvidence = null
);

public record OcrCandidate(
    Guid Id,
    int[] Pages,
    Dictionary<string, OcrField> Fields,
    List<OcrTax> Taxes,
    List<string> Warnings,
    List<SourceRegion>? Regions = null,
    string GroupingReason = "Unverified text boundary",
    decimal? BoundaryConfidence = null,
    Dictionary<string, List<OcrField>>? Alternatives = null,
    List<OcrTax>? TaxAlternatives = null
);

public record OcrResult(
    string Provider,
    string State,
    string Text,
    List<OcrPage> Pages,
    List<OcrCandidate> Candidates,
    int[]? UnassignedPages = null,
    string ExtractorVersion = "receipt-parser/3",
    string? EngineVersion = null,
    string? Languages = null
);

public record OcrProviderHealth(
    string Status,
    string ParserVersion,
    string? TextEngineVersion,
    string? ImageEngineVersion,
    string Languages,
    string[]? MissingLanguages = null,
    string NormalizationStatus = "Unknown",
    string ImageRotationStatus = "Unknown"
);

public record OcrProgress(string Stage, int CompletedPages, int TotalPages, int FailedPages);

public interface IDocumentExtractor
{
    string Version => "unknown";
    Task<OcrProviderHealth> Health(CancellationToken ct) =>
        Task.FromResult(new OcrProviderHealth("Unknown", Version, null, null, ""));
    Task<OcrResult> Extract(byte[] bytes, string name, string mime, string? companyCui, CancellationToken ct);
    Task<OcrResult> ExtractProgress(
        byte[] bytes,
        string name,
        string mime,
        string? companyCui,
        Func<OcrProgress, Task> progress,
        CancellationToken ct
    ) => Extract(bytes, name, mime, companyCui, ct);
}

public static class ReceiptParser
{
    static readonly Regex Amounts = new(
        @"(?<![\d])[-+]?\d+(?:[ .,\u00a0\u202f]\d{3})*[.,]\d{2}(?!\d)",
        RegexOptions.Compiled
    );

    public static decimal? Number(string text)
    {
        var s = text.Trim().Replace(" ", "").Replace("\u00a0", "").Replace("\u202f", "");
        var comma = s.LastIndexOf(',');
        var dot = s.LastIndexOf('.');
        if (comma > dot)
            s = s.Replace(".", "").Replace(',', '.');
        else
            s = s.Replace(",", "");
        return decimal.TryParse(
            s,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var n
        )
            ? n
            : null;
    }

    static Guid CandidateId(int page, string text, int ordinal, SourceRegion? region = null)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                page + "|" + ordinal + "|" + text + (region == null ? "" : "|" + Serialization.Write(region))
            )
        );
        return new Guid(hash.AsSpan(0, 16));
    }

    public static OcrResult Parse(List<OcrPage> pages, string? companyCui)
    {
        List<OcrCandidate> candidates = [];
        foreach (var page in pages.SelectMany(DocumentSegmentation.Panels))
        {
            var segments = Regex
                .Split(
                    page.Text,
                    @"(?im)(?=^\s*(?:BON\s+FISCAL|FACTUR[ĂA]\s+(?:FISCAL[ĂA]|NR)|INVOICE\s+(?:NO|NUMBER))\b)"
                )
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (segments.Count > 1 && !Regex.IsMatch(segments[0], @"(?i)TOTAL|FACTUR|INVOICE|BON\s+FISCAL"))
            {
                segments[1] = segments[0] + "\n" + segments[1];
                segments.RemoveAt(0);
            }
            var ordinal = 0;
            var wordOffset = 0;
            foreach (var text in segments)
            {
                ordinal++;
                var currentOffset = wordOffset;
                var wordCount = Regex.Matches(text, @"\S+").Count;
                wordOffset += wordCount;
                if (segments.Count > 1 && !Regex.IsMatch(text, @"(?i)TOTAL|FACTUR|INVOICE|BON\s+FISCAL"))
                    continue;
                Dictionary<string, OcrField> fields = [];
                List<OcrTax> taxes = [];
                List<string> warnings = [];
                if (page.RecognitionRotation != 0)
                    warnings.Add(
                        "ORIENTATION_REVIEW: Imaginea a necesitat o rotație de recunoaștere. Confirmă valorile pe original. / Recognition required image rotation. Confirm values against the original."
                    );
                void Put(string key, string? value, string evidence)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var geometry = DocumentGeometry.Find(page, evidence, currentOffset, wordCount);
                        fields[key] = new(
                            value,
                            page.Number,
                            evidence,
                            Confidence: geometry.Confidence,
                            Box: geometry.Box,
                            RawValue: evidence,
                            Source: page.Method
                        );
                    }
                }
                var detectedType = Regex.Match(text, @"(?i)FACTUR[ĂA]|INVOICE|BON\s+FISCAL");
                if (detectedType.Success)
                    Put(
                        "documentType",
                        Regex.IsMatch(detectedType.Value, @"(?i)FACTUR|INVOICE") ? "Invoice" : "Receipt",
                        detectedType.Value
                    );
                var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x != "").ToList();
                var description = lines.FirstOrDefault(l =>
                    Regex.IsMatch(l, @"(?i)^(?:DESCRIERE|DENUMIRE|DESCRIPTION|ITEM|SERVICII)\s*[:\-]\s*\S")
                );
                if (description != null)
                    Put(
                        "description",
                        Regex.Replace(
                            description,
                            @"(?i)^(?:DESCRIERE|DENUMIRE|DESCRIPTION|ITEM|SERVICII)\s*[:\-]\s*",
                            ""
                        )[
                            ..Math.Min(
                                240,
                                Regex
                                    .Replace(
                                        description,
                                        @"(?i)^(?:DESCRIERE|DENUMIRE|DESCRIPTION|ITEM|SERVICII)\s*[:\-]\s*",
                                        ""
                                    )
                                    .Length
                            )
                        ],
                        description
                    );
                // Distinguish issue date from due date before proposing a date.
                // A bare date is safe only when no competing printed dates exist.
                var dateCandidates = lines
                    .SelectMany((line, lineIndex) => Regex.Matches(line,
                        @"\b(\d{2}[./-]\d{2}[./-]\d{4}|\d{4}-\d{2}-\d{2})\b")
                        .Select(m => new { Match = m, Line = line, LineIndex = lineIndex }))
                    .ToList();
                var issueDates = dateCandidates.Where(x => Regex.IsMatch(x.Line,
                    @"(?i)\b(?:DATA\s*(?:EMITERII|EMISIEI|BONULUI|FACTURII)|EMIS(?:A|Ă)|ISSUE\s*DATE|INVOICE\s*DATE|DATE\s*OF\s*ISSUE)\b")
                    && !Regex.IsMatch(x.Line, @"(?i)\b(?:SCADEN[TȚ][AĂ]|DUE\s*DATE|PAY\s*BY)\b")).ToList();
                var eligibleDates = issueDates.Count > 0 ? issueDates : dateCandidates.Where(x =>
                    !Regex.IsMatch(x.Line, @"(?i)\b(?:SCADEN[TȚ][AĂ]|DUE\s*DATE|PAY\s*BY)\b")).ToList();
                var distinctDates = eligibleDates.Select(x => x.Match.Value).Distinct().ToList();
                var date = eligibleDates.Count > 0 ? eligibleDates[0].Match : Regex.Match("", @"\d+");
                if (distinctDates.Count > 1)
                {
                    var geometry = DocumentGeometry.Find(page, date.Value, currentOffset, wordCount);
                    fields["date"] = new(null, page.Number, string.Join(" / ", distinctDates),
                        "Ambiguous", geometry.Confidence, geometry.Box, date.Value, page.Method);
                    warnings.Add("DATE_MULTIPLE: Există mai multe date posibile. Selectează data emiterii. / Multiple candidate dates; select the issue date.");
                }
                DateOnly day = default;
                if (
                    distinctDates.Count == 1 && date.Success
                    && DateOnly.TryParseExact(
                        date.Value,
                        new[] { "dd.MM.yyyy", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out day
                    )
                )
                {
                    var parts = date.Value.Split('/');
                    if (
                        parts.Length == 3
                        && int.TryParse(parts[0], out var first)
                        && int.TryParse(parts[1], out var second)
                        && first <= 12
                        && second <= 12
                        && first != second
                    )
                    {
                        var geometry = DocumentGeometry.Find(page, date.Value, currentOffset, wordCount);
                        fields["date"] = new(
                            null,
                            page.Number,
                            date.Value,
                            "Ambiguous",
                            geometry.Confidence,
                            geometry.Box,
                            date.Value,
                            page.Method
                        );
                        warnings.Add(
                            "DATE_AMBIGUOUS: Confirmă manual ziua și luna de pe original. / Confirm the printed day and month manually."
                        );
                    }
                    else
                        Put("date", day.ToString("yyyy-MM-dd"), date.Value);
                }
                var cuiMatches = Regex
                    .Matches(text, @"(?i)\b(?:CUI|CIF|COD\s+FISCAL|VAT\s*(?:ID|NO))\s*[:#-]?\s*(RO\s*)?(\d{2,10})\b")
                    .Select(m => m)
                    .ToList();
                var cui = cuiMatches.Select(m => Rules.Cui(m.Groups[2].Value)).Distinct().ToList();
                // Never infer a tenant's fiscal identity from a sample CUI.
                // Without a configured company CUI, leave the company-VAT branch
                // unclassified and require explicit human review/configuration.
                var company = Rules.Cui(companyCui);
                var printedCompany = company.Length == 0
                    ? null
                    : Regex.Match(text, @"(?i)\bRO\s*" + Regex.Escape(company) + @"\b");
                var hasCompanyCui = (printedCompany?.Success ?? false) || (company != "" && cui.Contains(company));
                if (hasCompanyCui)
                    Put("hasCompanyCui", "true", printedCompany?.Success == true
                        ? printedCompany.Value
                        : cuiMatches.First(m => Rules.Cui(m.Groups[2].Value) == company).Value);
                var supplier =
                    cui.FirstOrDefault(x => x != company && Rules.ValidCui(x)) ?? cui.FirstOrDefault(x => x != company);
                if (supplier != null && !Rules.ValidCui(supplier))
                    warnings.Add(
                        "CUI_INVALID: CUI-ul citit nu trece verificarea. Verifică originalul. / Extracted VAT ID fails checksum. Check the original."
                    );
                if (supplier != null)
                {
                    Put("cui", supplier, cuiMatches.First(m => Rules.Cui(m.Groups[2].Value) == supplier).Value);
                    Put("country", "RO", cuiMatches.First(m => Rules.Cui(m.Groups[2].Value) == supplier).Value);
                }
                // Country inference uses explicit supplier-address clues, not a
                // generic guess from document language or invoice currency.
                var supplierBlock = string.Join(" ", lines.Take(12));
                var countryClues = new (string Code, string Pattern)[]
                {
                    ("DE", @"(?i)\b(?:GERMANY|DEUTSCHLAND|GERMANIA|BERLIN|HAMBURG|MÜNCHEN|MUNICH)\b"),
                    ("FR", @"(?i)\b(?:FRANCE|FRANȚA|FRANTA|PARIS|LYON)\b"),
                    ("IT", @"(?i)\b(?:ITALY|ITALIA|MILANO|MILAN|ROMA|ROME)\b"),
                    ("ES", @"(?i)\b(?:SPAIN|ESPANA|ESPAÑA|SPANIA|MADRID|BARCELONA)\b"),
                    ("AT", @"(?i)\b(?:AUSTRIA|ÖSTERREICH|VIENNA|WIEN|VIENA)\b"),
                    ("HU", @"(?i)\b(?:HUNGARY|UNGARIA|MAGYARORSZÁG|BUDAPEST)\b"),
                    ("GB", @"(?i)\b(?:UNITED KINGDOM|GREAT BRITAIN|LONDON|REGATUL UNIT)\b"),
                    ("US", @"(?i)\b(?:UNITED STATES|USA|NEW YORK|SAN FRANCISCO)\b"),
                    ("NL", @"(?i)\b(?:NETHERLANDS|OLANDA|AMSTERDAM)\b"),
                    ("BE", @"(?i)\b(?:BELGIUM|BELGIA|BRUSSELS|BRUXELLES)\b"),
                    ("CH", @"(?i)\b(?:SWITZERLAND|ELVEȚIA|ELVETIA|SCHWEIZ|ZÜRICH|ZURICH)\b"),
                    ("BG", @"(?i)\b(?:BULGARIA|SOFIA)\b"),
                    ("GR", @"(?i)\b(?:GREECE|GRECIA|ATHENS|ATENA)\b"),
                    ("PL", @"(?i)\b(?:POLAND|POLONIA|WARSZAWA|WARSAW)\b"),
                };
                var foreign = countryClues
                    .Select(c => (c.Code, Evidence: Regex.Match(supplierBlock, c.Pattern)))
                    .FirstOrDefault(c => c.Evidence.Success);
                if (foreign.Evidence != null && foreign.Evidence.Success)
                {
                    Put("country", foreign.Code, foreign.Evidence.Value);
                    if (hasCompanyCui)
                        warnings.Add("CUI-ul companiei apare într-un document de furnizor străin. Verifică țara. / Company CUI appears on a foreign document; verify supplier country.");
                }
                else if (hasCompanyCui || cui.Count > 0 || Regex.IsMatch(supplierBlock, @"(?i)\b(?:ROMANIA|ROMÂNIA|BUCUREȘTI|BUCURESTI|CLUJ-NAPOCA|TIMIȘOARA|TIMISOARA|IAȘI|IASI)\b"))
                {
                    if (!fields.ContainsKey("country"))
                        Put("country", "RO", "Romania / CUI RO");
                    if (!hasCompanyCui && company.Length > 0)
                    {
                        Put("hasCompanyCui", "false", "Configured company CUI " + company + " not detected");
                        warnings.Add("CUI-ul companiei configurate (" + company + ") nu a fost detectat; confirmă pe document. / Configured company CUI (" + company + ") was not detected; confirm against the source.");
                    }
                }
                if (company.Length == 0 && (cui.Count > 0 || fields.GetValueOrDefault("country")?.Value == "RO"))
                    warnings.Add("COMPANY_IDENTITY_NOT_CONFIGURED: Configurează CUI-ul companiei înainte de clasificarea fiscală. / Configure the company VAT ID before determining the VAT treatment.");
                if (company != "" && !hasCompanyCui && cui.Count > 0)
                    warnings.Add(
                        "CUI-ul companiei nu a fost detectat; confirmă pe original. / Company VAT ID was not detected; check the original."
                    );
                var invoice = Regex.Match(
                    text,
                    @"(?im)(?:FACTUR[ĂA](?:\s+FISCAL[ĂA])?|INVOICE|BON(?:\s+FISCAL)?|DOCUMENT)\s*(?:NR\.?|NO\.?|NUMBER|#)\s*[:#-]?\s*([A-Z0-9/-]+)"
                );
                if (invoice.Success)
                    Put("invoice", invoice.Groups[1].Value, invoice.Value);
                var currency = Regex.Match(
                    text,
                    @"\b(RON|LEI|EUR|USD|GBP|CHF|HUF|PLN|CZK|BGN|DKK|NOK|SEK)\b",
                    RegexOptions.IgnoreCase
                );
                if (currency.Success)
                    Put(
                        "currency",
                        currency.Value.ToUpperInvariant() == "LEI" ? "RON" : currency.Value.ToUpperInvariant(),
                        currency.Value
                    );
                else if (text.Contains('€'))
                    Put("currency", "EUR", "€");
                else
                {
                    var symbol = Regex.Match(text, @"[$£]");
                    if (symbol.Success)
                    {
                        var geometry = DocumentGeometry.Find(page, symbol.Value, currentOffset, wordCount);
                        fields["currency"] = new(
                            null,
                            page.Number,
                            symbol.Value,
                            "Ambiguous",
                            geometry.Confidence,
                            geometry.Box,
                            symbol.Value,
                            page.Method
                        );
                        warnings.Add(
                            "CURRENCY_AMBIGUOUS: Simbolul nu identifică sigur moneda. Selectează moneda de pe document. / Currency symbol is ambiguous. Select the document currency."
                        );
                    }
                }
                // A last matching number can be change, an advance, or a RON
                // equivalent. Prefer labelled payable/gross totals; ambiguous
                // alternatives need a human choice rather than a silent guess.
                var totalCandidates = lines
                    .Where(l => Regex.IsMatch(l,
                        @"(?i)\b(?:TOTAL|SUMA(?:\s+TOTAL[ĂA])?\s+(?:DE\s+)?(?:PLAT[ĂA]|ACHITAT[ĂA])|VALOARE(?:A)?\s+TOTAL[ĂA]|AMOUNT\s+DUE|GRAND\s+TOTAL)\b")
                        && !Regex.IsMatch(l,
                            @"(?i)SUB[ -]?TOTAL|TOTAL\s*(?:TVA|TAX|VAT|BAZ[ĂA])|REST|CHANGE|AVANS|ADVANCE|ECHIVALENT|EQUIVALENT|BALANCE\s+BROUGHT"))
                    .Select(l => new
                    {
                        Line = l,
                        Amounts = Amounts.Matches(l).Select(m => Number(m.Value))
                            .Where(v => v != null).Select(v => v!.Value).ToList(),
                        Score = Regex.IsMatch(l,
                            @"(?i)TOTAL\s*DE\s*PLAT[ĂA]|TOTAL\s*GENERAL|GRAND\s+TOTAL|AMOUNT\s+DUE|(?:DE\s+)?ACHITAT[ĂA]?")
                            ? 2 : 1
                    })
                    .Where(x => x.Amounts.Count > 0)
                    .ToList();
                if (totalCandidates.Count > 0)
                {
                    var bestScore = totalCandidates.Max(x => x.Score);
                    var best = totalCandidates.Where(x => x.Score == bestScore).ToList();
                    var alternatives = best.SelectMany(x => x.Amounts).Distinct().ToList();
                    if (alternatives.Count == 1)
                        Put("amount", alternatives[0].ToString("0.00", CultureInfo.InvariantCulture), best[0].Line);
                    else
                    {
                        var evidence = string.Join(" / ", best.Select(x => x.Line).Distinct());
                        var geometry = DocumentGeometry.Find(page, best[0].Line, currentOffset, wordCount);
                        fields["amount"] = new(null, page.Number, evidence, "Ambiguous",
                            geometry.Confidence, geometry.Box, evidence, page.Method);
                        warnings.Add("TOTAL_AMBIGUOUS: Sunt mai multe totaluri posibile. Confirmă suma de plată pe original. / Multiple possible totals; verify the payable amount.");
                    }
                }
                foreach (var line in lines)
                {
                    var rate = Regex.Match(line, @"(?<!\d)(\d{1,2}(?:[.,]\d+)?)\s*%");
                    if (!rate.Success)
                        continue;
                    if (!Regex.IsMatch(line, @"(?i)\b(?:TVA|VAT|TAX)\b"))
                    {
                        warnings.Add(
                            "VAT_AMOUNT_NOT_PRINTED: Cota nu dovedește TVA-ul plătit; verifică documentul. / A rate alone does not prove paid VAT; check the document."
                        );
                        continue;
                    }
                    var pct = Number(rate.Groups[1].Value);
                    if (pct == null)
                        continue;
                    if (pct is 5 or 19)
                        warnings.Add(
                            "VAT_RATE_UNSUPPORTED: Cotele 5% și 19% sunt dezactivate. Nu schimba automat cota citită. / Rates 5% and 19% are disabled. Do not automatically change the extracted rate."
                        );
                    var numbers = Amounts
                        .Matches(line[(rate.Index + rate.Length)..])
                        .Select(m => Number(m.Value))
                        .Where(n => n != null)
                        .Select(n => n!.Value)
                        .ToList();
                    if (numbers.Count >= 2)
                        taxes.Add(new(pct.Value, numbers[0], numbers[1], page.Number, line));
                    else if (numbers.Count == 1 && pct == 0)
                        taxes.Add(new(0, numbers[0], 0, page.Number, line));
                    else if (numbers.Count == 1 && Regex.IsMatch(line, @"(?i)TVA|VAT|TAX"))
                    {
                        var paid = numbers[0];
                        taxes.Add(new(pct.Value, Rules.Money(paid * 100 / pct.Value), paid, page.Number, line, Source: "Derived"));
                        warnings.Add(
                            "Baza TVA a fost dedusă din TVA; verifică baza exactă de pe document. / VAT base was derived; verify the exact printed base."
                        );
                    }
                }
                var name = lines.FirstOrDefault(l =>
                    l.Length >= 3
                    && !Regex.IsMatch(l, @"(?i)BON\s+FISCAL|FACTURA|INVOICE|CUI|CIF|TOTAL|\d{2}[./]\d{2}[./]\d{4}")
                );
                Put("supplier", name, name ?? "");
                if (taxes.Select(t => t.Rate).Distinct().Count() != taxes.Count)
                    warnings.Add(
                        "Mai multe valori pentru aceeași cotă; verifică și cumulează baza/TVA. / Multiple entries for one rate; verify and aggregate."
                    );
                warnings.Add(
                    "Propuneri OCR: verifică toate valorile pe original. / OCR suggestions: verify all values against the original."
                );
                if (taxes.Count == 0 && Regex.IsMatch(text, @"(?i)\b(?:TVA|VAT)\b"))
                    warnings.Add(
                        "VAT_UNCERTAIN: TVA-ul plătit nu a putut fi citit. Verifică originalul; nu presupune zero. / Paid VAT could not be read. Check the original; do not assume zero."
                    );
                taxes = taxes
                    .Select(t =>
                    {
                        var geometry = DocumentGeometry.Find(page, t.Evidence, currentOffset, wordCount);
                        return t with { Box = geometry.Box, Confidence = geometry.Confidence };
                    })
                    .ToList();
                if (fields.ContainsKey("amount") || fields.ContainsKey("invoice") || taxes.Count > 0)
                    candidates.Add(
                        new(
                            CandidateId(page.Number, text, ordinal, page.Region),
                            [page.Number],
                            fields,
                            taxes,
                            warnings,
                            DocumentSegmentation.RegionsFor(page, currentOffset, wordCount, segments.Count > 1)
                        )
                    );
            }
        }
        candidates = DocumentSegmentation.Group(pages, candidates);
        return new(
            "Tesseract",
            pages.Any(p => p.Error != null) ? "PartiallyProcessed"
                : candidates.Count > 0 ? "ReviewRequired"
                : "ManualEntryRequired",
            string.Join("\n\n", pages.Select(p => p.Text)),
            pages,
            candidates,
            pages.Where(p => !candidates.Any(c => c.Pages.Contains(p.Number))).Select(p => p.Number).ToArray()
        );
    }
}

public sealed class LocalDocumentExtractor(IConfiguration config) : IDocumentExtractor
{
    public string Version => "receipt-parser/3";
    OcrProviderHealth? cached;
    DateTime checkedAt;
    readonly SemaphoreSlim healthLock = new(1, 1);

    public static string[] MissingModels(string requested, string? installed) =>
        requested
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(language =>
                !(installed ?? "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(language, StringComparer.Ordinal)
            )
            .ToArray();

    public async Task<OcrProviderHealth> Health(CancellationToken ct)
    {
        await healthLock.WaitAsync(ct);
        try
        {
            if (cached != null && DateTime.UtcNow - checkedAt < TimeSpan.FromMinutes(1))
                return cached;
            async Task<string?> Check(string executable, string argument)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    return await Run(executable, [argument], timeout.Token, true);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    return null;
                }
            }
            var tools = await Task.WhenAll(
                Check(config["Ocr:PdfText"] ?? "pdftotext", "-v"),
                Check(config["Ocr:PdfInfo"] ?? "pdfinfo", "-v"),
                Check(config["Ocr:PdfRenderer"] ?? "pdftoppm", "-v"),
                Check(config["Ocr:ImageRotator"] ?? "convert", "-version")
            );
            var paddle = await PaddleOcrClient.Health(config, ct);
            string? First(string? value) =>
                value
                    ?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
            var text = First(tools[0]);
            var image = paddle.Engine;
            const string languages = "ro+en";
            var missing = MissingModels(languages, string.Join("\n", paddle.Languages));
            var normalization = tools[1] != null && tools[2] != null ? "Ready" : "Unavailable";
            var ready =
                text != null && image != null && missing.Length == 0 && normalization == "Ready" && tools[3] != null;
            cached = new(
                ready ? "Ready" : image == null ? "Unavailable" : "Partial",
                Version,
                text,
                image,
                languages,
                missing,
                normalization,
                tools[3] != null ? "Ready" : "Unavailable"
            );
            checkedAt = DateTime.UtcNow;
            return cached;
        }
        finally
        {
            healthLock.Release();
        }
    }

    public Task<OcrResult> Extract(byte[] bytes, string name, string mime, string? companyCui, CancellationToken ct) =>
        ExtractProgress(bytes, name, mime, companyCui, _ => Task.CompletedTask, ct);

    public async Task<OcrResult> ExtractProgress(
        byte[] bytes,
        string name,
        string mime,
        string? companyCui,
        Func<OcrProgress, Task> progress,
        CancellationToken ct
    )
    {
        var temp = Path.Combine(Path.GetTempPath(), "decont-ocr-" + Guid.NewGuid());
        Directory.CreateDirectory(temp);
        try
        {
            var input = Path.Combine(temp, "original" + Path.GetExtension(name));
            await File.WriteAllBytesAsync(input, bytes, ct);
            await progress(new("Normalizing", 0, 0, 0));
            Dictionary<int, int> rotations = [];
            List<string> images = [];
            if (mime == "application/pdf")
            {
                var info = await Run(config["Ocr:PdfInfo"] ?? "pdfinfo", [input], ct);
                var count = Regex.Match(info, @"(?m)^Pages:\s*(\d+)");
                Guard.Require(
                    count.Success && int.Parse(count.Groups[1].Value) <= (config.GetValue<int?>("Ocr:MaxPages") ?? 50),
                    "ocr_pages",
                    "Fișierul depășește limita OCR. Completează manual sau separă documentele.",
                    "File exceeds the OCR page limit. Enter manually or separate documents."
                );
                var metadata = await Run(
                    config["Ocr:PdfInfo"] ?? "pdfinfo",
                    ["-f", "1", "-l", count.Groups[1].Value, input],
                    ct
                );
                foreach (
                    System.Text.RegularExpressions.Match rotation in Regex.Matches(
                        metadata,
                        @"(?m)^Page\s+(\d+) rot:\s*(-?\d+)"
                    )
                )
                    rotations[int.Parse(rotation.Groups[1].Value)] = int.Parse(rotation.Groups[2].Value);
                await progress(new("Rendering", 0, int.Parse(count.Groups[1].Value), 0));
                images = Enumerable
                    .Range(1, int.Parse(count.Groups[1].Value))
                    .Select(n => Path.Combine(temp, "page-" + n + ".png"))
                    .ToList();
            }
            else if (mime == "image/heic")
            {
                var output = Path.Combine(temp, "image.png");
                await Run(config["Ocr:HeicConverter"] ?? "heif-convert", [input, output], ct);
                images.Add(output);
            }
            else
                images.Add(input);
            Guard.Require(
                images.Count <= (config.GetValue<int?>("Ocr:MaxPages") ?? 50),
                "ocr_pages",
                "Fișierul depășește limita OCR. Completează manual sau separă documentele.",
                "File exceeds the OCR page limit. Enter manually or separate documents."
            );
            List<OcrPage> pages = [];
            await progress(new("Extracting", 0, images.Count, 0));
            for (int i = 0; i < images.Count; i++)
            {
                try
                {
                    OcrPage? page = null;
                    if (mime == "application/pdf")
                    {
                        try
                        {
                            var layout = await Run(
                                config["Ocr:PdfText"] ?? "pdftotext",
                                ["-f", (i + 1).ToString(), "-l", (i + 1).ToString(), "-bbox-layout", input, "-"],
                                ct
                            );
                            page = DocumentGeometry.PdfBbox(i + 1, layout);
                            if (string.IsNullOrWhiteSpace(page.Text))
                                page = null;
                        }
                        catch (Exception) when (!ct.IsCancellationRequested)
                        {
                            page = null;
                        }
                    }
                    if (page == null)
                    {
                        if (mime == "application/pdf")
                        {
                            await progress(new("Rendering", i, images.Count, pages.Count(p => p.Error != null)));
                            await Run(
                                config["Ocr:PdfRenderer"] ?? "pdftoppm",
                                [
                                    "-f",
                                    (i + 1).ToString(),
                                    "-l",
                                    (i + 1).ToString(),
                                    "-singlefile",
                                    "-scale-to",
                                    "2400",
                                    "-png",
                                    input,
                                    Path.Combine(temp, "page-" + (i + 1)),
                                ],
                                ct
                            );
                            await progress(new("Extracting", i, images.Count, pages.Count(p => p.Error != null)));
                        }
                        page = await DocumentOrientation.Recover(
                            await PaddleOcrClient.Recognize(config, images[i], i + 1, ct),
                            images[i],
                            config,
                            ct
                        );
                    }
                    Guard.Require(
                        page.Text.Length <= 200000,
                        "ocr_text_limit",
                        "Pagina are prea mult text. Verifică manual.",
                        "Page text exceeds processing limits. Review manually."
                    );
                    pages.Add(page with { Rotation = rotations.GetValueOrDefault(i + 1) });
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    pages.Add(new(i + 1, "", "ExtractionFailed"));
                }
                if (mime == "application/pdf" && File.Exists(images[i]))
                    File.Delete(images[i]);
                await progress(new("Extracting", i + 1, images.Count, pages.Count(p => p.Error != null)));
            }
            await progress(new("Validating", images.Count, images.Count, pages.Count(p => p.Error != null)));
            var result = ReceiptParser.Parse(pages, companyCui);
            var health = await Health(ct);
            return result with
            {
                Provider = "PaddleOCR",
                EngineVersion = string.Join(
                    " | ",
                    new[]
                    {
                        pages.Any(p => p.Method == "TextLayer") ? health.TextEngineVersion : null,
                        pages.Any(p => p.Method == "ImageOcr") ? health.ImageEngineVersion : null,
                    }.Where(v => v != null)
                ),
                Languages = health.Languages,
            };
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    public static async Task<string> Run(
        string exe,
        IEnumerable<string> args,
        CancellationToken ct,
        bool includeError = false
    )
    {
        using var p = new Process();
        p.StartInfo = new(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
            p.StartInfo.ArgumentList.Add(a);
        p.Start();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromMinutes(3));
        var stdout = p.StandardOutput.ReadToEndAsync(limit.Token);
        var stderr = p.StandardError.ReadToEndAsync(limit.Token);
        try
        {
            await p.WaitForExitAsync(limit.Token);
        }
        catch
        {
            if (!p.HasExited)
                p.Kill(true);
            throw;
        }
        var error = await stderr;
        Guard.Require(
            p.ExitCode == 0,
            "ocr",
            "Extragerea a eșuat. Poți completa manual.",
            "Extraction failed. You can enter manually."
        );
        var output = await stdout;
        return includeError ? output + "\n" + error : output;
    }
}

public static class DocumentCapture
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost(
            "/reports/{id:guid}/capture",
            async (Guid id, HttpContext c, Db db, Reports svc, IStore store) =>
                await ReceiptFiles.Upload(id, null, c, db, svc, store)
        );
        api.MapPost(
            "/reports/{id:guid}/documents/{document:guid}/extract",
            async (Guid id, Guid document, VersionRequest i, HttpContext c, Db db, Reports svc) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        IOriginalDocument? file =
                            r.Attachments.FirstOrDefault(e => e.Id == document && !e.Archived)
                            ?? (IOriginalDocument?)
                                r
                                    .Lines.SelectMany(l => l.Evidence)
                                    .FirstOrDefault(e => e.Id == document && !e.Archived);
                        Guard.Require(
                            file != null && file.Scan == "Clean",
                            "scan",
                            "Documentul trebuie să fie verificat.",
                            "Document must be scanned."
                        );
                        Guard.Require(
                            !await db.Jobs.AnyAsync(j =>
                                j.Json == document.ToString()
                                && (j.Kind == "Ocr" || j.Kind == "OcrReport")
                                && (j.State == "Pending" || j.State == "Processing")
                            ),
                            "pending",
                            "Extragerea este deja în curs.",
                            "Extraction is already queued."
                        );
                        file!.Ocr = Serialization.Write(new { state = "Pending" });
                        db.Jobs.Add(
                            new() { Kind = file is ReportAttachment ? "OcrReport" : "Ocr", Json = document.ToString() }
                        );
                        Reports.Touch(r);
                        svc.Log(c.User, id, document.ToString(), "ExtractionRequested", reason: i.Reason);
                        return Api.View(r);
                    }
                )
        );
    }

    public static void Link(Report r, Line line, Guid? document, Reports svc, HttpContext c, string? reason = null)
    {
        if (document == null)
            return;
        var file = r.Attachments.SingleOrDefault(e => e.Id == document && !e.Archived);
        Guard.Require(
            file != null && file.Scan == "Clean",
            "source",
            "Originalul OCR nu este disponibil sau verificat.",
            "OCR original is unavailable or unscanned."
        );
        var ids = Serialization.Read<List<Guid>>(file!.CoveredLines);
        if (!ids.Contains(line.Id))
            ids.Add(line.Id);
        file.CoveredLines = Serialization.Write(ids);
        svc.Log(
            c.User,
            r.Id,
            line.Id.ToString(),
            "OcrSuggestionsReviewed",
            after: new { document, line.Json },
            reason: reason
        );
    }
}
