using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record CurrencyFinancePolicy(
    int Exponent = 2,
    int RateDecimals = 6,
    InvoiceRounding Rounding = InvoiceRounding.HalfAwayFromZero,
    decimal RoundingToleranceRon = 2m,
    string? EstimateRate = null
);

public class FinancePolicyVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Currency { get; set; } = "";
    public int Version { get; set; }
    public DateOnly From { get; set; }
    public string Json { get; set; } = "{}";
    public string Actor { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class ExpenseFxProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LineId { get; set; }
    public string InputHash { get; set; } = "";
    public string Source { get; set; } = "";
    public string? Rate { get; set; }
    public string? RonAmount { get; set; }
    public Guid? TransactionId { get; set; }
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class ExpenseFxDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LineId { get; set; }
    public Guid? ProposalId { get; set; }
    public string InputHash { get; set; } = "";
    public decimal FinalRon { get; set; }
    public string Source { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class ExpenseCalculation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Guid LineId { get; set; }
    public string InputHash { get; set; } = "";
    public string ContextHash { get; set; } = "";
    public string Policy { get; set; } = "";
    public string Result { get; set; } = "";
    public string PostingJson { get; set; } = "";
    public string OutputHash { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public record FinancePolicyInput(string Currency, DateOnly From, CurrencyFinancePolicy Policy, string Reason);

public record FxProposalInput(
    long Version,
    string Source,
    string? Rate,
    string? RonAmount,
    Guid? TransactionId,
    string Reason
);

public record FxDecisionInput(long Version, Guid ProposalId, string Reason);

public record AcceptFinancials(long Version, string? ExceptionPolicy, string Reason);

public record FinancialPreview(
    ExpenseData PostingData,
    ReconstructionResult? Vat,
    string Applicability,
    decimal RoundingRon,
    string InputHash,
    string ContextHash,
    List<Problem> Issues,
    CurrencyFinancePolicy Policy,
    decimal EstimatedRon,
    decimal FinalRon,
    string FxSource,
    TaxPolicySelection? VatPolicy = null
);

public static class FinancialCalculations
{
    public const string Version = "financial-calculation/3-source-preserving";
    public const string OwnerPolicy =
        "OWNER-2026-11-05: original VAT bases; Finance-only FX/rounding; 2RON rounding; signed warnings; no FX reapproval";

    public static string SourceHash(Line l) => Serialization.Hash(l.Json);

    public static string Stamp(Line l) =>
        Serialization.Hash(
            Serialization.Write(
                new
                {
                    l.Json,
                    estimatedRon = l.EstimatedRon.ToString("G29", CultureInfo.InvariantCulture),
                    settledRon = l.SettledRon?.ToString("G29", CultureInfo.InvariantCulture),
                    l.Reconciled,
                    financeRon = l.FinanceRon?.ToString("G29", CultureInfo.InvariantCulture),
                    l.FxDecisionId,
                    l.FxVersion,
                    evidence = l
                        .Evidence.OrderBy(e => e.Id)
                        .Select(e => new
                        {
                            e.Id,
                            e.Hash,
                            e.Archived,
                            e.Scan,
                        }),
                }
            )
        );

    public static CurrencyFinancePolicy DefaultPolicy(string currency) => new();

    public static async Task<(CurrencyFinancePolicy Policy, string Reference)> Policy(
        Db db,
        string currency,
        DateOnly date
    )
    {
        var versions = await db.FinancePolicies.Where(p => p.Currency == currency && p.From <= date).ToListAsync();
        var last = versions.OrderByDescending(p => p.Version).FirstOrDefault();
        return last == null
            ? (DefaultPolicy(currency), OwnerPolicy)
            : (Serialization.Read<CurrencyFinancePolicy>(last.Json), last.Id + "/" + last.Version);
    }

    public static Master? LatestEstimate(IEnumerable<Master> masters, string currency) =>
        masters
            .Where(m =>
                m.Kind == "fx" && m.Code == currency && Rules.Effective(m, DateOnly.FromDateTime(DateTime.UtcNow))
            )
            .OrderByDescending(m => m.From)
            .ThenByDescending(m => m.Id)
            .FirstOrDefault();

    public static async Task<(decimal Rate, string Reference)> Estimate(Db db, string currency)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var versions = await db.FinancePolicies.Where(p => p.Currency == currency && p.From <= today).ToListAsync();
        var entered = versions
            .OrderByDescending(p => p.Version)
            .FirstOrDefault(p =>
                InvoiceArithmetic.Parse(Serialization.Read<CurrencyFinancePolicy>(p.Json).EstimateRate) != null
            );
        var master = LatestEstimate(await db.Masters.ToListAsync(), currency);
        DateTime? masterEntered = null;
        if (master != null)
        {
            var id = master.Id.ToString();
            masterEntered = await db
                .Audits.Where(a => a.Entity == id && a.Action == "MasterVersionCreated")
                .Select(a => (DateTime?)a.At)
                .MaxAsync();
        }
        if (entered != null && (master == null || masterEntered == null || entered.At >= masterEntered))
            return (
                InvoiceArithmetic.Parse(Serialization.Read<CurrencyFinancePolicy>(entered.Json).EstimateRate)!.Value,
                entered.Id.ToString()
            );
        Guard.Require(
            master != null && Rules.Number(master, "ronRate") > 0,
            "fx",
            "Finance trebuie să configureze un curs estimativ pentru monedă.",
            "Finance must configure an estimated exchange rate for the currency."
        );
        return (Rules.Number(master!, "ronRate"), master!.Id.ToString());
    }

    public static void Invalidate(Line l, bool fx = false)
    {
        l.CalculationId = null;
        l.CalculatedJson = null;
        l.CalculationHash = null;
        l.RoundingRon = 0;
        l.FinancialRounding = null;
        if (fx)
        {
            l.FinanceRon = null;
            l.FxDecisionId = null;
        }
    }

    public static string Context(IEnumerable<Master> masters, string policy) =>
        Serialization.Hash(
            Version
                + policy
                + Serialization.Write(
                    masters
                        .Where(m =>
                            m.Kind
                                is "vat"
                                    or "accounting"
                                    or "costCenters"
                                    or "projects"
                                    or "suppliers"
                                    or "erpSettings"
                                    or "fieldRules"
                                    or "policies"
                        )
                        .OrderBy(m => m.Id)
                        .Select(m => new
                        {
                            m.Id,
                            m.Json,
                            m.From,
                            m.To,
                            m.Active,
                        })
                )
        );

    public static FinancialPreview Preview(Line l, List<Master> masters, CurrencyFinancePolicy policy, string reference)
    {
        var d = l.Data();
        decimal Ron(decimal value) => InvoiceArithmetic.Quantize(value, 2, policy.Rounding);
        var issues = new List<Problem>();
        void Add(string code, string ro, string en, bool blocking = true) =>
            issues.Add(new(code, ro, en, l.Id, blocking));
        if (d.Amount == 0)
            Add("amount", "Suma nu poate fi zero.", "Amount cannot be zero.");
        if (d.Amount < 0)
            Add(
                "negative_amount",
                "Sumă negativă: verifică sensul operațiunii și documentul. Se folosește același flux.",
                "Negative amount: check transaction direction and evidence. The same workflow applies.",
                false
            );
        ReconstructionResult? result = null;
        var posting = d;
        var applicability =
            d.Country != "RO" ? "FOREIGN_NO_ROMANIAN_VAT_RECOVERY"
            : !Rules.DomesticVat(d) ? "WITHOUT_COMPANY_CUI_NO_ROMANIAN_VAT_RECOVERY"
            : "DOMESTIC_DOCUMENTED_VAT";
        decimal difference = 0;
        if (Rules.DomesticVat(d) && d.VatTreatment == "STANDARD")
        {
            if (d.TaxCurrency != d.Currency)
                Add(
                    "vat_currency",
                    "TVA trebuie introdus în moneda originală a documentului. Verifică valorile istorice.",
                    "Enter VAT in the document original currency. Review legacy converted values."
                );
            var components = new List<PrintedVatComponent>();
            foreach (var tax in d.Taxes)
            {
                var rate = masters.SingleOrDefault(m =>
                    m.Id == tax.RateId && m.Kind == "vat" && Rules.Effective(m, DateOnly.Parse(d.Date))
                );
                if (rate == null)
                {
                    Add("vat_rate", "Cota TVA nu este valabilă.", "VAT rate is not effective.");
                    continue;
                }
                components.Add(
                    new(
                        (int)(Rules.Number(rate, "rate") * 100),
                        tax.Vat.ToString(CultureInfo.InvariantCulture),
                        tax.Base.ToString(CultureInfo.InvariantCulture),
                        tax.State ?? (tax.Vat == 0 ? VatEvidenceState.ExplicitZero : VatEvidenceState.PrintedAmount),
                        tax.EvidenceReference ?? "expense/" + l.Id
                    )
                );
            }
            var factor = d.Amount == 0 ? 1 : Math.Abs(l.Final() / d.Amount);
            var tolerance = policy.RoundingToleranceRon / (factor == 0 ? 1 : factor);
            var unit = InvoiceArithmetic.Unit(policy.Exponent);
            var minor = (int)Math.Min(100000000, decimal.Floor(tolerance / unit));
            result = InvoiceArithmetic.Calculate(
                new(d.Date, d.Currency, d.Amount.ToString(CultureInfo.InvariantCulture), components),
                new(reference, 1, d.Currency, policy.Exponent, policy.Rounding, minor, minor, true, false, OwnerPolicy)
            );
            foreach (var reason in result.Reasons)
                Add(
                    reason.Code switch
                    {
                        "GROSS_VARIANCE" => "vat_total",
                        "PRINTED_BASE_VARIANCE" => "vat_arithmetic",
                        "UNSUPPORTED_TAX_RATE" => "vat_rate",
                        "DUPLICATE_TAX_RATE" => "vat_duplicate",
                        "ZERO_RATE_VAT" => "vat_arithmetic",
                        _ => reason.Code.ToLowerInvariant(),
                    },
                    reason.Ro,
                    reason.En,
                    reason.Blocking
                );
            var taxes = d.Taxes.ToList();
            posting = d with { Taxes = taxes, TaxCurrency = d.Currency };
            difference = Ron(
                l.Final()
                    - taxes.Sum(t =>
                        Ron(t.Base * (d.Amount == 0 ? 0 : l.Final() / d.Amount))
                        + Ron(t.Vat * (d.Amount == 0 ? 0 : l.Final() / d.Amount))
                    )
            );
            if (Math.Abs(difference) > policy.RoundingToleranceRon && !issues.Any(i => i.Code == "vat_total"))
                Add(
                    "vat_total",
                    "Diferența RON depășește toleranța Finance. Verifică documentul și cursul.",
                    "RON difference exceeds Finance tolerance. Check the document and exchange rate."
                );
            if (difference != 0 && Math.Abs(difference) <= policy.RoundingToleranceRon)
                Add(
                    "rounding_adjustment",
                    "Diferența de rotunjire este înregistrată separat; nu devine bază 0%.",
                    "Rounding difference is recorded separately; it does not become zero-rated base.",
                    false
                );
        }
        else if (d.Taxes.Count > 0)
            Add(
                "vat_na",
                "Păstrează TVA-ul străin în dovezi; nu îl contabiliza drept TVA românesc.",
                "Keep foreign VAT in source evidence; do not book Romanian input VAT."
            );
        if (d.VatTreatment != "STANDARD")
        {
            applicability = d.VatTreatment;
            if (d.VatTreatment is not ("EXEMPT" or "OUTSIDE_SCOPE" or "NOT_PRESENT"))
                Add(
                    "vat_unknown",
                    "Tratamentul TVA este necunoscut. Verifică documentul.",
                    "VAT treatment is unknown. Review the document."
                );
            if (string.IsNullOrWhiteSpace(d.VatExplanation))
                Add(
                    "vat_explanation",
                    "Explică tratamentul TVA și indică documentul justificativ.",
                    "Explain VAT treatment and reference supporting evidence."
                );
        }
        return new(
            posting,
            result,
            applicability,
            difference,
            Stamp(l),
            Context(masters, reference + Serialization.Write(policy)),
            issues,
            policy,
            l.EstimatedRon,
            l.Final(),
            l.FxDecisionId != null ? "FINANCE_APPROVED"
                : l.Reconciled ? "BANK_MATCH_PROPOSAL"
                : "ESTIMATE"
        );
    }

    public static async Task<FinancialPreview> Preview(Db db, Line l)
    {
        var (p, reference) = await Policy(db, l.Data().Currency, DateOnly.Parse(l.Data().Date));
        var accounts = await db.SupplierAccounts.OrderBy(a => a.Id).ToListAsync();
        var bundles = await db.ReportAttachments.Where(a => a.ReportId == l.ReportId).OrderBy(a => a.Id).ToListAsync();
        var groups = await db.DocumentGroups.Where(g => g.ExpenseId == l.Id).OrderBy(g => g.Id).ToListAsync();
        var provenance = Serialization.Write(
            new
            {
                bundles = bundles
                    .Where(a => a.Covers(l.Id))
                    .Select(a => new
                    {
                        a.Id,
                        a.Hash,
                        a.Archived,
                        a.Scan,
                        a.CoveredLines,
                    }),
                groups = groups.Select(g => new
                {
                    g.Id,
                    g.BundleId,
                    g.Regions,
                    g.State,
                    g.VerifiedAt,
                    g.VerifiedBy,
                    g.CorrectedExpense,
                }),
            }
        );
        var selection = await TaxPolicies.Resolve(db, l.Data());
        var preview = Preview(
            l,
            await db.Masters.ToListAsync(),
            p,
            reference + Serialization.Write(accounts) + provenance + selection.Reference
        );
        var issues = preview.Issues;
        issues.AddRange(selection.Issues.Select(i => i with { LineId = l.Id }));
        var rule = selection.Rule;
        var data = l.Data();
        if (data.VatTreatment != "STANDARD" && (rule == null || rule.Treatment != data.VatTreatment))
            issues.Add(
                new(
                    "tax_treatment_policy",
                    "Tratamentul TVA necesită o politică aprobată care îl permite explicit.",
                    "VAT treatment needs an approved policy explicitly permitting it.",
                    l.Id
                )
            );
        if (rule != null)
        {
            if (rule.Treatment != data.VatTreatment)
                issues.Add(
                    new(
                        "tax_treatment_policy",
                        "Tratamentul documentului nu corespunde politicii aprobate.",
                        "Document treatment does not match approved policy.",
                        l.Id
                    )
                );
            if (rule.InputVatRequired && data.Taxes.Count == 0)
                issues.Add(
                    new(
                        "vat_missing",
                        "Politica solicită TVA tipărit pe document.",
                        "Policy requires printed VAT components.",
                        l.Id
                    )
                );
            if (!rule.AllowZeroBase && data.Taxes.Any(t => preview.Vat?.Components.Any(c => c.RateBps == 0) == true))
                issues.Add(
                    new(
                        "zero_policy",
                        "Politica nu permite bază 0% pentru acest document.",
                        "Policy does not permit a zero-rated base for this document.",
                        l.Id
                    )
                );
        }
        return preview with { VatPolicy = selection };
    }

    public static async Task Accept(
        Db db,
        Reports svc,
        Report r,
        Line l,
        ClaimsPrincipal user,
        string reason,
        string? exception = null
    )
    {
        Guard.Require(
            user.IsInRole("Finance") && States.FinanceEdit(r.State),
            "forbidden",
            "Doar Finance poate confirma calculele.",
            "Only Finance can accept financial calculations.",
            403
        );
        Guard.Reason(reason);
        var fx =
            l.FxDecisionId == null
                ? null
                : db.FxDecisions.Local.FirstOrDefault(d => d.Id == l.FxDecisionId)
                    ?? await db.FxDecisions.FindAsync(l.FxDecisionId);
        Guard.Require(
            l.Data().Currency == "RON"
                || fx != null && fx.LineId == l.Id && fx.InputHash == SourceHash(l) && fx.FinalRon == l.FinanceRon,
            "fx_unresolved",
            "Finance trebuie să aleagă valoarea finală RON: estimare, bancă sau ajustare manuală.",
            "Finance must select final RON amount: estimate, bank or manual adjustment."
        );
        Guard.Require(
            l.Evidence.Any(e => !e.Archived && e.Scan == "Clean")
                || r.Attachments.Any(a => !a.Archived && a.Scan == "Clean" && a.Covers(l.Id)),
            "financial_evidence",
            "Încarcă și verifică documentul justificativ înainte de confirmarea bazei și totalului.",
            "Upload and scan the source document before confirming base and total."
        );
        var preview = await Preview(db, l);
        var blocked = preview.Issues.Where(i => i.Blocking).ToList();
        Guard.Require(
            blocked.Count == 0,
            "financial_validation",
            Serialization.Write(preview.Issues),
            Serialization.Write(preview.Issues)
        );
        var sourceIds = r
            .Attachments.Where(a => !a.Archived && a.Covers(l.Id))
            .Select(a => a.Id)
            .Concat(l.Evidence.Where(e => !e.Archived).Select(e => e.Id))
            .ToList();
        var groups = await db
            .DocumentGroups.Where(g => g.ExpenseId == l.Id)
            .Select(g => new
            {
                g.Id,
                g.BundleId,
                g.Regions,
                g.VerifiedAt,
                g.VerifiedBy,
            })
            .ToListAsync();
        var sourceDocuments = r
            .Attachments.Where(a => sourceIds.Contains(a.Id))
            .Cast<IOriginalDocument>()
            .Concat(l.Evidence.Where(a => sourceIds.Contains(a.Id)))
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Hash,
                a.Mime,
            })
            .ToList();
        var mappings = (await db.Masters.OrderBy(m => m.Id).ToListAsync())
            .Where(m =>
                m.Kind
                    is "vat"
                        or "accounting"
                        or "costCenters"
                        or "projects"
                        or "suppliers"
                        or "erpSettings"
                        or "fieldRules"
                        or "policies"
                        or "fx"
            )
            .ToList();
        var supplierAccounts = await db.SupplierAccounts.OrderBy(a => a.Id).ToListAsync();
        var taxPolicy = await TaxPolicies.Resolve(db, l.Data());
        var original = l.Data();
        var fxProposal = fx?.ProposalId == null ? null : await db.FxProposals.FindAsync(fx.ProposalId);
        var currencyVersions = await db
            .FinancePolicies.Where(p => p.Currency == original.Currency)
            .OrderBy(p => p.Version)
            .ToListAsync();
        var transactionIds = await db.Matches.Where(m => m.LineId == l.Id).Select(m => m.TransactionId).ToListAsync();
        var bankSources = await db
            .BankTreatments.Where(b =>
                transactionIds.Contains(b.TransactionId) && !db.BankTreatmentRevocations.Any(v => v.TreatmentId == b.Id)
            )
            .ToListAsync();
        var bankTransactions = await db.Transactions.Where(t => transactionIds.Contains(t.Id)).ToListAsync();
        var json = Serialization.Write(
            new
            {
                preview,
                original,
                sourceIds,
                sourceDocuments,
                groups,
                mappings,
                supplierAccounts,
                taxPolicy,
                fxDecision = fx,
                fxProposal,
                currencyVersions,
                bankSources,
                bankTransactions,
                estimate = new { l.EstimatedRon, l.FxVersion },
                exception,
                reason,
                fxDecisionId = l.FxDecisionId,
                calculator = Version,
                arithmetic = InvoiceArithmetic.Version,
            }
        );
        var calculation = new ExpenseCalculation
        {
            ReportId = r.Id,
            LineId = l.Id,
            InputHash = preview.InputHash,
            ContextHash = preview.ContextHash,
            Policy = Serialization.Write(preview.Policy),
            Result = json,
            PostingJson = Serialization.Write(preview.PostingData),
            OutputHash = Serialization.Hash(json),
            Actor = Reports.Actor(user),
        };
        db.Calculations.Add(calculation);
        foreach (var component in preview.Vat?.Components ?? [])
        {
            var input = l.Data()
                .Taxes.Single(t =>
                    mappings.Any(m => m.Id == t.RateId && (int)(Rules.Number(m, "rate") * 100) == component.RateBps)
                );
            var linked = await db.DocumentGroups.Where(g => g.ExpenseId == l.Id).ToListAsync();
            Guard.Require(
                input.EvidencePage == null
                    || input.EvidencePage > 0
                        && linked.Any(g => OcrReviews.Regions(g).Any(r => r.Page == input.EvidencePage)),
                "vat_evidence_page",
                "Pagina TVA trebuie să aparțină grupului de documente verificat al bonului.",
                "VAT page must belong to the receipt verified document group."
            );
            db.VerifiedVatComponents.Add(
                new()
                {
                    CalculationId = calculation.Id,
                    LineId = l.Id,
                    RateBps = component.RateBps,
                    Json = Serialization.Write(
                        new
                        {
                            component,
                            input,
                            sourceDocuments,
                            regions = linked.Select(g => new
                            {
                                g.Id,
                                g.Regions,
                                g.VerifiedAt,
                                g.VerifiedBy,
                            }),
                            verifiedBy = Reports.Actor(user),
                            verifiedAt = calculation.At,
                        }
                    ),
                    PolicyReference = taxPolicy.Reference,
                }
            );
        }
        l.CalculationId = calculation.Id;
        l.CalculationHash = preview.InputHash;
        l.CalculatedJson = calculation.PostingJson;
        l.RoundingRon = preview.RoundingRon;
        l.FinancialRounding = preview.Policy.Rounding;
        svc.Log(
            user,
            r.Id,
            l.Id.ToString(),
            "FinancialCalculationAccepted",
            after: new
            {
                calculation.Id,
                calculation.InputHash,
                calculation.ContextHash,
                calculation.OutputHash,
                exception,
            },
            reason: reason
        );
    }

    public static async Task<List<Problem>> ExportProblems(Db db, Report r)
    {
        List<Problem> issues = [];
        foreach (var l in r.Lines.Where(l => !l.Archived))
        {
            var preview = await Preview(db, l);
            var calculation =
                l.CalculationId == null
                    ? null
                    : db.Calculations.Local.FirstOrDefault(x => x.Id == l.CalculationId)
                        ?? await db.Calculations.FindAsync(l.CalculationId);
            if (
                calculation == null
                || calculation.ReportId != r.Id
                || calculation.LineId != l.Id
                || calculation.InputHash != Stamp(l)
                || calculation.ContextHash != preview.ContextHash
                || calculation.PostingJson != l.CalculatedJson
                || calculation.OutputHash != Serialization.Hash(calculation.Result)
                || l.FinancialRounding != preview.Policy.Rounding
            )
                issues.Add(
                    new(
                        "calculation_stale",
                        "Confirmă din nou calculul Finance; datele sau politica s-au modificat.",
                        "Accept Finance calculation again; inputs or policy changed.",
                        l.Id
                    )
                );
            var fx =
                l.FxDecisionId == null
                    ? null
                    : db.FxDecisions.Local.FirstOrDefault(d => d.Id == l.FxDecisionId)
                        ?? await db.FxDecisions.FindAsync(l.FxDecisionId);
            if (
                l.Data().Currency != "RON"
                && (fx == null || fx.LineId != l.Id || fx.InputHash != SourceHash(l) || fx.FinalRon != l.FinanceRon)
            )
                issues.Add(
                    new(
                        "fx_unresolved",
                        "Finance trebuie să confirme valoarea finală în RON.",
                        "Finance must confirm final RON value.",
                        l.Id
                    )
                );
        }
        return issues;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
                "/finance/currency-policies",
                async (Db db) => await db.FinancePolicies.OrderByDescending(p => p.At).ToListAsync()
            )
            .RequireAuthorization(p => p.RequireRole("Finance"));
        api.MapPost(
                "/finance/currency-policies",
                async (FinancePolicyInput i, Db db, Reports svc, HttpContext c) =>
                    await Api.Atomic(
                        db,
                        c,
                        i,
                        async () =>
                        {
                            Guard.Reason(i.Reason);
                            Guard.Require(
                                i.Currency.Length == 3
                                    && i.Currency.All(char.IsAsciiLetterUpper)
                                    && i.Policy.Exponent is >= 0 and <= 6
                                    && i.Policy.RateDecimals is >= 0 and <= 12
                                    && Enum.IsDefined(i.Policy.Rounding)
                                    && i.Policy.RoundingToleranceRon is >= 0 and <= 2,
                                "policy",
                                "Politică invalidă. Toleranța este între 0 și 2 RON.",
                                "Invalid policy. Tolerance must be between 0 and 2 RON."
                            );
                            Guard.Require(
                                i.Policy.EstimateRate == null
                                    || InvoiceArithmetic.Parse(i.Policy.EstimateRate) is decimal estimate
                                        && estimate > 0
                                        && estimate <= 1000000
                                        && decimal.Round(estimate, i.Policy.RateDecimals) == estimate,
                                "fx_precision",
                                "Curs estimativ invalid.",
                                "Invalid estimated rate."
                            );
                            // A policy's effective end is derived from the next
                            // From-date. Equal From-dates have no deterministic
                            // winner; serializable Atomic plus this predicate
                            // prevents concurrent overlapping versions.
                            Guard.Require(
                                !await db.FinancePolicies.AnyAsync(p =>
                                    p.Currency == i.Currency && p.From == i.From),
                                "policy_overlap",
                                "Există deja o politică pentru această monedă și dată. Închide sau revizuiește versiunea existentă.",
                                "A policy for this currency/effective date already exists.", 409
                            );
                            var version =
                                (
                                    await db
                                        .FinancePolicies.Where(p => p.Currency == i.Currency)
                                        .MaxAsync(p => (int?)p.Version)
                                    ?? 0
                                ) + 1;
                            var policy = new FinancePolicyVersion
                            {
                                Currency = i.Currency,
                                Version = version,
                                From = i.From,
                                Json = Serialization.Write(i.Policy),
                                Actor = Reports.Actor(c.User),
                                Reason = i.Reason,
                            };
                            db.FinancePolicies.Add(policy);
                            svc.Log(
                                c.User,
                                null,
                                policy.Id.ToString(),
                                "FinanceCurrencyPolicyVersioned",
                                after: policy,
                                reason: i.Reason
                            );
                            return policy;
                        }
                    )
            )
            .RequireAuthorization(p => p.RequireRole("Finance"));
        api.MapGet(
            "/reports/{id:guid}/financials",
            async (Guid id, Db db, Reports svc, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                var rows = new List<object>();
                foreach (var l in r.Lines.Where(l => !l.Archived))
                    rows.Add(
                        new
                        {
                            l.Id,
                            l.Position,
                            preview = await Preview(db, l),
                            l.CalculationId,
                            l.FxDecisionId,
                            bankMatches = await db
                                .Matches.Where(m => m.LineId == l.Id)
                                .Select(m => new
                                {
                                    m.TransactionId,
                                    m.Ron,
                                    m.Amount,
                                })
                                .ToListAsync(),
                            proposals = await db
                                .FxProposals.Where(p => p.LineId == l.Id)
                                .OrderByDescending(p => p.At)
                                .ToListAsync(),
                            decisions = await db
                                .FxDecisions.Where(p => p.LineId == l.Id)
                                .OrderByDescending(p => p.At)
                                .ToListAsync(),
                            calculations = await db
                                .Calculations.Where(p => p.LineId == l.Id)
                                .OrderByDescending(p => p.At)
                                .Select(p => new
                                {
                                    p.Id,
                                    p.At,
                                    p.Actor,
                                    p.OutputHash,
                                })
                                .ToListAsync(),
                        }
                    );
                return rows;
            }
        );
        api.MapPost(
            "/reports/{id:guid}/lines/{line:guid}/calculate-preview",
            async (Guid id, Guid line, Db db, Reports svc, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                return await Preview(db, r.Lines.Single(l => l.Id == line && !l.Archived));
            }
        );
        var finance = api.MapGroup("/finance/reports/{id:guid}/lines/{line:guid}")
            .RequireAuthorization(p => p.RequireRole("Finance"));
        finance.MapPost(
            "/verify-financials",
            async (Guid id, Guid line, AcceptFinancials i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        Guard.Require(
                            States.FinanceEdit(r.State),
                            "state",
                            "Ajustările Finance sunt disponibile după aprobarea managerului.",
                            "Finance adjustments are available after manager approval."
                        );
                        svc.Edit(r, c.User, i.Reason);
                        await Accept(
                            db,
                            svc,
                            r,
                            r.Lines.Single(l => l.Id == line && !l.Archived),
                            c.User,
                            i.Reason,
                            i.ExceptionPolicy
                        );
                        Reports.Touch(r);
                        return Api.View(r);
                    }
                )
        );
        finance.MapPost(
            "/fx-proposals",
            async (Guid id, Guid line, FxProposalInput i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        Guard.Require(
                            States.FinanceEdit(r.State),
                            "state",
                            "Ajustările Finance sunt disponibile după aprobarea managerului.",
                            "Finance adjustments are available after manager approval."
                        );
                        svc.Edit(r, c.User, i.Reason);
                        var l = r.Lines.Single(l => l.Id == line && !l.Archived);
                        Guard.Require(
                            l.Data().Currency != "RON" && (i.Source is "MANUAL" or "BANK_STATEMENT" or "ESTIMATE"),
                            "fx_source",
                            "Sursa FX nu este validă.",
                            "Invalid FX source."
                        );
                        var (policy, _) = await Policy(db, l.Data().Currency, DateOnly.Parse(l.Data().Date));
                        decimal? rate = InvoiceArithmetic.Parse(i.Rate),
                            ron = InvoiceArithmetic.Parse(i.RonAmount);
                        if (i.Source == "ESTIMATE")
                        {
                            ron = l.EstimatedRon;
                            rate = null;
                        }
                        if (i.Source == "BANK_STATEMENT")
                        {
                            var transaction = await db.Transactions.FindAsync(i.TransactionId);
                            Guard.Require(
                                transaction != null
                                    && l.Data().CardId == transaction.CardId
                                    && l.Data().Currency == transaction.Currency,
                                "bank",
                                "Tranzacția nu corespunde cardului/monedei.",
                                "Transaction does not match card/currency."
                            );
                            var allocations = await db.Matches.Where(m => m.LineId == l.Id).ToListAsync();
                            var allocatedTransactionIds = allocations.Select(a => a.TransactionId).Distinct().ToList();
                            var treatments = await db
                                .BankTreatments.Where(b =>
                                    allocatedTransactionIds.Contains(b.TransactionId)
                                    && !db.BankTreatmentRevocations.Any(r => r.TreatmentId == b.Id)
                                )
                                .ToListAsync();
                            Guard.Require(
                                allocatedTransactionIds.Count > 0
                                    && treatments.Select(t => t.TransactionId).Distinct().Count()
                                        == allocatedTransactionIds.Count,
                                "bank_classification",
                                "Confirmă principalul și eventualele costuri ale extrasului înainte de alegerea valorii bancare. Dacă ai alocat deja tranzacția, anulează alocarea și reclasifică.",
                                "Confirm statement principal and any charges before choosing bank FX. Undo existing allocations before classifying."
                            );
                            var allocated = allocations.Sum(m => m.Ron);
                            Guard.Require(
                                l.Reconciled
                                    && allocated != 0
                                    && allocations.Any(m => m.TransactionId == i.TransactionId),
                                "bank_allocation",
                                "Asociază și reconciliază tranzacția înainte de selectarea valorii bancare.",
                                "Allocate and reconcile the bank transaction before selecting its amount."
                            );
                            ron = allocated;
                            rate = null;
                        }
                        Guard.Require(
                            (rate != null) ^ (ron != null),
                            "fx_value",
                            "Completează fie cursul, fie suma RON.",
                            "Provide either rate or RON amount."
                        );
                        if (rate != null)
                            Guard.Require(
                                rate > 0 && rate <= 1000000 && decimal.Round(rate.Value, policy.RateDecimals) == rate,
                                "fx_precision",
                                "Cursul nu respectă precizia Finance.",
                                "Rate does not match Finance precision."
                            );
                        if (ron != null)
                            Guard.Require(
                                ron != 0
                                    && Math.Sign(ron.Value) == Math.Sign(l.Data().Amount)
                                    && Rules.Money(ron.Value) == ron,
                                "fx_sign",
                                "Suma RON trebuie să aibă semnul documentului și două zecimale.",
                                "RON amount must match document sign and two-decimal precision."
                            );
                        var p = new ExpenseFxProposal
                        {
                            LineId = l.Id,
                            InputHash = SourceHash(l),
                            Source = i.Source,
                            Rate = rate?.ToString(CultureInfo.InvariantCulture),
                            RonAmount = ron?.ToString(CultureInfo.InvariantCulture),
                            TransactionId = i.TransactionId,
                            Reason = i.Reason,
                            Actor = Reports.Actor(c.User),
                        };
                        db.FxProposals.Add(p);
                        Reports.Touch(r);
                        svc.Log(c.User, id, line.ToString(), "FxProposed", after: p, reason: i.Reason);
                        return new { proposal = p, report = Api.View(r) };
                    }
                )
        );
        finance.MapPost(
            "/fx-decisions",
            async (Guid id, Guid line, FxDecisionInput i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        Guard.Require(
                            States.FinanceEdit(r.State),
                            "state",
                            "Ajustările Finance sunt disponibile după aprobarea managerului.",
                            "Finance adjustments are available after manager approval."
                        );
                        svc.Edit(r, c.User, i.Reason);
                        var l = r.Lines.Single(l => l.Id == line && !l.Archived);
                        var p = await db.FxProposals.SingleOrDefaultAsync(p =>
                            p.Id == i.ProposalId && p.LineId == line
                        );
                        Guard.Require(
                            p != null && p.InputHash == SourceHash(l),
                            "fx_stale",
                            "Propunerea nu mai corespunde documentului. Creează una nouă.",
                            "Proposal no longer matches source. Create another.",
                            409
                        );
                        if (p!.Source == "BANK_STATEMENT")
                            Guard.Require(
                                l.Reconciled
                                    && (await db.Matches.Where(m => m.LineId == l.Id).ToListAsync()).Sum(m => m.Ron)
                                        == InvoiceArithmetic.Parse(p.RonAmount)
                                    && await db.Matches.AnyAsync(m =>
                                        m.LineId == l.Id && m.TransactionId == p.TransactionId
                                    ),
                                "bank_stale",
                                "Asocierea bancară s-a modificat.",
                                "Bank allocation changed.",
                                409
                            );
                        var currentPolicy = (await Policy(db, l.Data().Currency, DateOnly.Parse(l.Data().Date))).Policy;
                        var final =
                            p.RonAmount != null
                                ? decimal.Parse(p.RonAmount, CultureInfo.InvariantCulture)
                                : InvoiceArithmetic.Quantize(
                                    l.Data().Amount * decimal.Parse(p.Rate!, CultureInfo.InvariantCulture),
                                    2,
                                    currentPolicy.Rounding
                                );
                        var decision = new ExpenseFxDecision
                        {
                            LineId = line,
                            ProposalId = p.Id,
                            InputHash = p.InputHash,
                            FinalRon = final,
                            Source = p.Source,
                            Reason = i.Reason,
                            Actor = Reports.Actor(c.User),
                        };
                        db.FxDecisions.Add(decision);
                        var before = new
                        {
                            l.EstimatedRon,
                            final = l.Final(),
                            l.FxDecisionId,
                        };
                        Invalidate(l);
                        l.FinanceRon = final;
                        l.FxDecisionId = decision.Id;
                        Reports.Touch(r);
                        svc.Log(
                            c.User,
                            id,
                            line.ToString(),
                            "FxApprovedWithoutManagerReapproval",
                            before,
                            new
                            {
                                decision.Id,
                                decision.FinalRon,
                                estimated = l.EstimatedRon,
                            },
                            i.Reason
                        );
                        return Api.View(r);
                    }
                )
        );
    }
}
