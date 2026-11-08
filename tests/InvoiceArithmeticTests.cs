using Decont;
using Xunit;

namespace Decont.Tests;

public class InvoiceArithmeticTests
{
    static ReconstructionPolicy Policy(int exponent = 2) =>
        new("TEST-ONLY-POLICY", 1, "RON", exponent, InvoiceRounding.HalfAwayFromZero, 1, 1, false, false, null);

    static PrintedVatComponent Vat(int rate, string? amount, string? basis = null) =>
        new(rate, amount, basis, VatEvidenceState.PrintedAmount, "synthetic-source/page1");

    static PrintedInvoice Invoice(string gross, params PrintedVatComponent[] taxes) =>
        new("2026-10-15", "RON", gross, taxes);

    [Theory]
    [InlineData(2100, "21.00", "121.00")]
    [InlineData(1100, "11.00", "111.00")]
    public void BasesAreReconstructedFromPrintedVatInOriginalCurrency(int rate, string vat, string gross)
    {
        var result = InvoiceArithmetic.Calculate(Invoice(gross, Vat(rate, vat)), Policy());
        Assert.False(result.HasBlockingReasons);
        Assert.Equal("100.00", Assert.Single(result.Components).RoundedBase);
        Assert.Equal("0", result.Variance);
        Assert.False(result.ReadyForBooking);
    }

    [Fact]
    public void MixedRatesAndDocumentedZeroBaseDoNotDoubleBook()
    {
        var mixed = InvoiceArithmetic.Calculate(Invoice("232.00", Vat(1100, "11.00"), Vat(2100, "21.00")), Policy());
        Assert.False(mixed.HasBlockingReasons);
        Assert.Equal("232.00", mixed.ReconstructedGross);
        var zero = InvoiceArithmetic.Calculate(
            Invoice(
                "171.00",
                Vat(2100, "21.00"),
                new PrintedVatComponent(0, "0.00", "50.00", VatEvidenceState.ExplicitZero, "printed-zero-line")
            ),
            Policy()
        );
        Assert.False(zero.HasBlockingReasons);
        Assert.Equal("171.00", zero.ReconstructedGross);
    }

    [Fact]
    public void MisreadVatIsNotAdjustedToForceGross()
    {
        var r = InvoiceArithmetic.Calculate(Invoice("121.00", Vat(2100, "12.00")), Policy());
        Assert.Equal("57.14", r.Components[0].RoundedBase);
        Assert.Equal("51.86", r.Variance);
        Assert.Equal("12.00", r.Components[0].RawVat);
        Assert.Contains(r.Reasons, e => e.Code == "GROSS_VARIANCE" && e.Blocking);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1900)]
    public void ExplicitUserDisabledRatesCannotPass(int rate)
    {
        var r = InvoiceArithmetic.Calculate(Invoice("121.00", Vat(rate, "21.00")), Policy());
        Assert.Contains(r.Reasons, e => e.Code == "UNSUPPORTED_TAX_RATE");
    }

    [Theory]
    [InlineData(VatEvidenceState.Unknown)]
    [InlineData(VatEvidenceState.NotPresent)]
    public void MissingVatRemainsUnknown(VatEvidenceState state)
    {
        var r = InvoiceArithmetic.Calculate(
            Invoice("121.00", new PrintedVatComponent(2100, null, null, state, "source")),
            Policy()
        );
        Assert.Null(r.ReconstructedGross);
        Assert.Null(r.Components[0].PaidVat);
        Assert.Contains(r.Reasons, e => e.Code == "VAT_UNKNOWN");
    }

    [Fact]
    public void UnexplainedResidualIsNeverInventedAsZeroBase()
    {
        var r = InvoiceArithmetic.Calculate(Invoice("171.00", Vat(2100, "21.00")), Policy());
        Assert.Single(r.Components);
        Assert.Equal("50", r.Variance);
        Assert.Contains(r.Reasons, e => e.Code == "GROSS_VARIANCE");
    }

    [Fact]
    public void CurrencyAndRoundingAreExplicitPolicyInputs()
    {
        Assert.Equal(2.35m, InvoiceArithmetic.Quantize(2.345m, 2, InvoiceRounding.HalfAwayFromZero));
        Assert.Equal(2.34m, InvoiceArithmetic.Quantize(2.345m, 2, InvoiceRounding.HalfToEven));
        Assert.Equal(-2.35m, InvoiceArithmetic.Quantize(-2.345m, 2, InvoiceRounding.HalfAwayFromZero));
        var input = Invoice("121.000", Vat(2100, "21.000")) with { Currency = "EUR" };
        var r = InvoiceArithmetic.Calculate(input, Policy(3) with { Currency = "EUR" });
        Assert.Equal("100.000", r.Components[0].RoundedBase);
        Assert.Equal("121.000", r.ReconstructedGross);
        Assert.False(r.HasBlockingReasons);
    }

    [Fact]
    public void CreditNotesRequireASeparateAuthorisedPolicy()
    {
        var input = Invoice("-121.00", Vat(2100, "-21.00"));
        Assert.Contains(InvoiceArithmetic.Calculate(input, Policy()).Reasons, r => r.Code == "CREDIT_POLICY_REQUIRED");
        var proposed = InvoiceArithmetic.Calculate(input, Policy() with { AllowCredits = true });
        Assert.Contains(proposed.Reasons, r => r.Code == "POLICY_AUTHORITY_REQUIRED");
        var authorised = InvoiceArithmetic.Calculate(
            input,
            Policy() with
            {
                AllowCredits = true,
                AuthorityReference = "SYNTHETIC-APPROVAL",
            }
        );
        Assert.False(authorised.HasBlockingReasons);
        Assert.Equal("-100.00", authorised.Components[0].RoundedBase);
    }

    [Fact]
    public void InputOrderDoesNotChangeFinancialOutcomeOrFingerprint()
    {
        var a = Invoice("232.00", Vat(1100, "11.00"), Vat(2100, "21.00"));
        var b = a with { Components = a.Components.Reverse().ToArray() };
        Assert.Equal(
            InvoiceArithmetic.Calculate(a, Policy()).OutputHash,
            InvoiceArithmetic.Calculate(b, Policy()).OutputHash
        );
        Assert.NotEqual(
            InvoiceArithmetic.Calculate(a, Policy()).PolicyHash,
            InvoiceArithmetic.Calculate(a, Policy() with { Version = 2 }).PolicyHash
        );
    }

    [Fact]
    public void GeneratedPositiveAndCreditPairsPreserveSignAndTotals()
    {
        for (var k = 1; k <= 250; k++)
        {
            var gross = (121m * k).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var vat = (21m * k).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var positive = InvoiceArithmetic.Calculate(Invoice(gross, Vat(2100, vat)), Policy());
            var negative = InvoiceArithmetic.Calculate(
                Invoice("-" + gross, Vat(2100, "-" + vat)),
                Policy() with
                {
                    AllowCredits = true,
                    AuthorityReference = "TEST-ONLY",
                }
            );
            Assert.False(positive.HasBlockingReasons);
            Assert.False(negative.HasBlockingReasons);
            Assert.Equal(
                decimal.Parse(positive.ReconstructedGross!, System.Globalization.CultureInfo.InvariantCulture),
                -decimal.Parse(negative.ReconstructedGross!, System.Globalization.CultureInfo.InvariantCulture)
            );
        }
    }

    [Theory]
    [InlineData("1,234.56")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("")]
    [InlineData("1.2345678901234")]
    public void BookedArithmeticCannotAcceptFloatOrAmbiguousText(string value) =>
        Assert.Null(InvoiceArithmetic.Parse(value));
}
