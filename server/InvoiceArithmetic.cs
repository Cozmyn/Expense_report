using System.Globalization;
using System.Text.RegularExpressions;

namespace Decont;

// Policy-neutral arithmetic. This does not approve a tax policy or replace live posting.
public enum InvoiceRounding
{
    HalfAwayFromZero,
    HalfToEven,
}

public enum VatEvidenceState
{
    Unknown,
    NotPresent,
    ExplicitZero,
    PrintedAmount,
    Exempt,
    OutsideScope,
}

public record ReconstructionPolicy(
    string Id,
    int Version,
    string Currency,
    int CurrencyExponent,
    InvoiceRounding Rounding,
    int GrossToleranceMinor,
    int PrintedBaseToleranceMinor,
    bool AllowCredits,
    bool AllowZeroResidual,
    string? AuthorityReference
);

public record PrintedVatComponent(
    int RateBps,
    string? PaidVat,
    string? PrintedBase,
    VatEvidenceState EvidenceState,
    string? EvidenceReference
);

public record PrintedInvoice(
    string DocumentDate,
    string Currency,
    string Gross,
    IReadOnlyList<PrintedVatComponent> Components
);

public record FinancialReason(string Code, string Ro, string En, bool Blocking = true);

public record ReconstructedComponent(
    int RateBps,
    string? RawVat,
    string? RawBase,
    VatEvidenceState EvidenceState,
    string? EvidenceReference,
    string? ExactBase,
    string? RoundedBase,
    string? PaidVat,
    string? BaseComparisonVariance
);

public record ReconstructionResult(
    string CalculatorVersion,
    string InputHash,
    string PolicyHash,
    string Currency,
    int CurrencyExponent,
    string RawGross,
    string? Gross,
    IReadOnlyList<ReconstructedComponent> Components,
    string? ReconstructedGross,
    string? Variance,
    IReadOnlyList<FinancialReason> Reasons,
    string OutputHash
)
{
    public bool HasBlockingReasons => Reasons.Any(r => r.Blocking);
    public bool ReadyForBooking => false; // Requires an accepted calculation ledger and approved policy.
}

public static class InvoiceArithmetic
{
    public const string Version = "invoice-arithmetic/1";
    static readonly Regex Canonical = new(
        @"^-?\d{1,18}(?:\.\d{1,12})?$",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1)
    );

    public static decimal? Parse(string? value) =>
        value != null
        && Canonical.IsMatch(value)
        && decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var amount
        )
            ? amount
            : null;

    static string Exact(decimal amount) => amount.ToString("G29", CultureInfo.InvariantCulture);

    public static decimal Unit(int exponent)
    {
        decimal value = 1;
        for (var i = 0; i < exponent; i++)
            value /= 10;
        return value;
    }

    public static decimal Quantize(decimal amount, int exponent, InvoiceRounding rounding) =>
        decimal.Round(
            amount,
            exponent,
            rounding == InvoiceRounding.HalfAwayFromZero ? MidpointRounding.AwayFromZero : MidpointRounding.ToEven
        );

    static string Money(decimal value, ReconstructionPolicy p) =>
        Quantize(value, p.CurrencyExponent, p.Rounding)
            .ToString("F" + p.CurrencyExponent, CultureInfo.InvariantCulture);

    public static ReconstructionResult Calculate(PrintedInvoice input, ReconstructionPolicy policy)
    {
        Guard.Require(
            input.Components != null && input.Components.Count <= 20,
            "calculation_input",
            "Prea multe componente TVA.",
            "Too many VAT components."
        );
        Guard.Require(
            policy.CurrencyExponent is >= 0 and <= 6
                && Enum.IsDefined(policy.Rounding)
                && policy.GrossToleranceMinor is >= 0 and <= 100000000
                && policy.PrintedBaseToleranceMinor is >= 0 and <= 100000000
                && policy.Version > 0
                && !string.IsNullOrWhiteSpace(policy.Id),
            "calculation_policy",
            "Politica de calcul nu este validă.",
            "Invalid calculation policy."
        );
        List<FinancialReason> reasons = [];
        List<ReconstructedComponent> components = [];
        void Block(string code, string ro, string en) => reasons.Add(new(code, ro, en));
        if (!DateOnly.TryParseExact(input.DocumentDate, "yyyy-MM-dd", out _))
            Block("INVALID_DATE", "Data documentului nu este validă.", "Invalid document date.");
        if (
            input.Currency != policy.Currency
            || input.Currency.Length != 3
            || !input.Currency.All(char.IsAsciiLetterUpper)
        )
            Block("CURRENCY_POLICY_MISMATCH", "Moneda nu corespunde politicii.", "Currency does not match policy.");
        if ((policy.AllowCredits || policy.AllowZeroResidual) && string.IsNullOrWhiteSpace(policy.AuthorityReference))
            Block(
                "POLICY_AUTHORITY_REQUIRED",
                "Lipsește referința aprobării politicii pentru excepție.",
                "An exception policy approval reference is required."
            );
        var gross = Parse(input.Gross);
        var quantum = Unit(policy.CurrencyExponent);
        decimal total = 0;
        var complete = true;
        if (gross == null || gross == 0 || Math.Abs(gross.Value) > 1000000000000m)
        {
            Block(
                "INVALID_GROSS",
                "Totalul trebuie să fie o sumă zecimală documentată, în limita permisă.",
                "Gross must be a documented decimal amount within limits."
            );
            complete = false;
        }
        else
        {
            if (Quantize(gross.Value, policy.CurrencyExponent, policy.Rounding) != gross)
                Block(
                    "CURRENCY_PRECISION",
                    "Totalul are mai multe zecimale decât permite moneda.",
                    "Gross exceeds currency precision."
                );
            if (gross < 0 && !policy.AllowCredits)
                Block(
                    "CREDIT_POLICY_REQUIRED",
                    "Notele de credit necesită o politică aprobată.",
                    "Credit notes require an approved policy."
                );
        }
        if (policy.AllowZeroResidual)
            Block(
                "ZERO_RESIDUAL_POLICY_PENDING",
                "Alocarea rezidualului la 0% necesită decizia explicită a beneficiarului și un flux aprobat.",
                "Zero-rate residual allocation requires the owner decision and an approved workflow."
            );
        var seen = new HashSet<int>();
        foreach (var source in input.Components.OrderBy(c => c.RateBps))
        {
            decimal? exactBase = null,
                roundedBase = null,
                paid = Parse(source.PaidVat),
                printedBase = Parse(source.PrintedBase),
                baseVariance = null;
            if (!Enum.IsDefined(source.EvidenceState))
            {
                Block("INVALID_EVIDENCE_STATE", "Starea dovezii TVA nu este validă.", "Invalid VAT evidence state.");
                complete = false;
            }
            if (!seen.Add(source.RateBps))
                Block("DUPLICATE_TAX_RATE", "Cota TVA este completată de două ori.", "VAT rate is entered twice.");
            if (source.RateBps is 500 or 1900 || source.RateBps is not (0 or 1100 or 2100))
            {
                Block(
                    "UNSUPPORTED_TAX_RATE",
                    "Cota nu este permisă. 5% și 19% sunt dezactivate.",
                    "Rate is unsupported. Rates 5% and 19% are disabled."
                );
                complete = false;
            }
            else if (source.EvidenceState is VatEvidenceState.Unknown or VatEvidenceState.NotPresent)
            {
                Block(
                    "VAT_UNKNOWN",
                    "TVA-ul plătit lipsește sau nu este confirmat; nu se presupune zero.",
                    "Paid VAT is missing or unconfirmed; zero is not assumed."
                );
                complete = false;
            }
            else if (source.EvidenceState is VatEvidenceState.Exempt or VatEvidenceState.OutsideScope)
            {
                Block(
                    "APPLICABILITY_POLICY_REQUIRED",
                    "Tratamentul scutit sau în afara sferei necesită o politică distinctă.",
                    "Exempt or outside-scope treatment requires a separate applicability policy."
                );
                complete = false;
            }
            else if (paid == null || source.PrintedBase != null && printedBase == null)
            {
                Block(
                    "INVALID_DECIMAL",
                    "Completează sumele ca valori zecimale exacte.",
                    "Amounts must be exact decimal values."
                );
                complete = false;
            }
            else if (
                Math.Abs(paid.Value) > 1000000000000m
                || printedBase != null && Math.Abs(printedBase.Value) > 1000000000000m
            )
            {
                Block("AMOUNT_RANGE", "Suma depășește limita permisă.", "Amount exceeds limits.");
                complete = false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(source.EvidenceReference))
                    Block(
                        "VAT_EVIDENCE_REQUIRED",
                        "Leagă componenta TVA de documentul justificativ.",
                        "Link the VAT component to source evidence."
                    );
                if (Quantize(paid.Value, policy.CurrencyExponent, policy.Rounding) != paid)
                    Block(
                        "CURRENCY_PRECISION",
                        "TVA-ul depășește precizia monedei.",
                        "VAT exceeds currency precision."
                    );
                if (
                    gross != null
                    && (gross > 0 && (paid < 0 || printedBase < 0) || gross < 0 && (paid > 0 || printedBase > 0))
                )
                    Block(
                        "SIGN_MISMATCH",
                        "Semnul bazei/TVA nu corespunde totalului.",
                        "Base/VAT sign does not match gross."
                    );
                if (source.EvidenceState == VatEvidenceState.ExplicitZero && paid != 0)
                    Block(
                        "ZERO_STATE_MISMATCH",
                        "Starea zero explicit nu poate avea TVA diferit de zero.",
                        "Explicit-zero evidence cannot contain nonzero VAT."
                    );
                if (source.RateBps == 0)
                {
                    if (paid != 0)
                        Block("ZERO_RATE_VAT", "La 0%, TVA trebuie să fie zero.", "VAT must be zero at 0%.");
                    if (printedBase == null)
                    {
                        Block(
                            "ZERO_BASE_EVIDENCE_REQUIRED",
                            "Completează baza 0% documentată; lipsa nu înseamnă bază zero.",
                            "Enter the documented zero-rate base; absence does not mean zero base."
                        );
                        complete = false;
                    }
                    else
                        exactBase = printedBase;
                }
                else
                    exactBase = paid.Value * 10000m / source.RateBps;
                if (exactBase != null)
                {
                    roundedBase = Quantize(exactBase.Value, policy.CurrencyExponent, policy.Rounding);
                    total += roundedBase.Value + paid.Value;
                    if (printedBase != null)
                    {
                        baseVariance = printedBase - roundedBase;
                        if (Math.Abs(baseVariance.Value) > policy.PrintedBaseToleranceMinor * quantum)
                            Block(
                                "PRINTED_BASE_VARIANCE",
                                "Baza tipărită diferă de baza reconstruită. Verifică documentul.",
                                "Printed base differs from reconstructed base. Check the source."
                            );
                    }
                }
            }
            components.Add(
                new(
                    source.RateBps,
                    source.PaidVat,
                    source.PrintedBase,
                    source.EvidenceState,
                    source.EvidenceReference,
                    exactBase == null ? null : Exact(exactBase.Value),
                    roundedBase == null ? null : Money(roundedBase.Value, policy),
                    paid == null ? null : Exact(paid.Value),
                    baseVariance == null ? null : Exact(baseVariance.Value)
                )
            );
        }
        if (input.Components.Count == 0)
        {
            Block(
                "VAT_UNKNOWN",
                "Nu există componente TVA documentate; este necesar un tratament fiscal explicit.",
                "No documented VAT components; an explicit tax treatment is required."
            );
            complete = false;
        }
        decimal? variance = complete && gross != null ? gross - total : null;
        // Residuals are never silently converted into zero-rate evidence, even when a future policy permits them.
        if (variance != null && variance != 0)
        {
            if (Math.Abs(variance.Value) > policy.GrossToleranceMinor * quantum)
                Block(
                    "GROSS_VARIANCE",
                    "Totalul documentului nu se reconciliază cu baza și TVA-ul reconstruit.",
                    "Document gross does not reconcile with reconstructed base and VAT."
                );
            else
                reasons.Add(
                    new(
                        "ROUNDING_VARIANCE",
                        "Diferență de rotunjire în limita politicii; suma tipărită rămâne neschimbată.",
                        "Rounding difference within policy; printed amount is unchanged.",
                        false
                    )
                );
        }
        var inputHash = Serialization.Hash(
            Serialization.Write(input with { Components = input.Components.OrderBy(c => c.RateBps).ToList() })
        );
        var policyHash = Serialization.Hash(Serialization.Write(policy));
        var result = new ReconstructionResult(
            Version,
            inputHash,
            policyHash,
            input.Currency,
            policy.CurrencyExponent,
            input.Gross,
            gross == null ? null : Money(gross.Value, policy),
            components,
            complete ? Money(total, policy) : null,
            variance == null ? null : Exact(variance.Value),
            reasons,
            ""
        );
        return result with { OutputHash = Serialization.Hash(Serialization.Write(result)) };
    }
}
