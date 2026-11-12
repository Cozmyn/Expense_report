using Decont;
using Xunit;

namespace Decont.Tests;

public class ReceiptParserSemanticTests
{
    private static OcrCandidate Candidate(string content)
    {
        var result = ReceiptParser.Parse([new OcrPage(1, content)], null);
        return Assert.Single(result.Candidates);
    }

    [Fact]
    public void DueDateDoesNotReplaceIssueDate()
    {
        var receipt = Candidate("FACTURA NR 112\nDATA SCADENTA 20.12.2026\nDATA EMITERII 11.11.2026\nTOTAL DE PLATA 120,00 RON");
        Assert.Equal("2026-11-11", receipt.Fields["date"].Value);
    }

    [Fact]
    public void ChangeAndRonEquivalentDoNotReplacePayableAmount()
    {
        var receipt = Candidate("BON FISCAL NR 77\nTOTAL DE PLATA 100,00 EUR\nECHIVALENT RON 500,00\nREST 10,00");
        Assert.Equal("100.00", receipt.Fields["amount"].Value);
    }

    [Fact]
    public void TwoDistinctPayableAmountsRemainUnconfirmed()
    {
        var receipt = Candidate("BON FISCAL NR 77\nTOTAL DE PLATA 100,00 RON\nTOTAL DE PLATA 110,00 RON");
        Assert.Null(receipt.Fields["amount"].Value);
        Assert.Equal("Ambiguous", receipt.Fields["amount"].State);
        Assert.Contains(receipt.Warnings, w => w.StartsWith("TOTAL_AMBIGUOUS:"));
    }

    [Fact]
    public void MultipleUnlabelledDatesRequireConfirmation()
    {
        var receipt = Candidate("BON FISCAL NR 77\n11.11.2026\n12.11.2026\nTOTAL 99,99 RON");
        Assert.Null(receipt.Fields["date"].Value);
        Assert.Contains(receipt.Warnings, w => w.StartsWith("DATE_MULTIPLE:"));
    }
}
