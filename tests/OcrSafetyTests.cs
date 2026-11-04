using Decont;using Xunit;
namespace Decont.Tests;
public class OcrSafetyTests {
 [Fact]public void CandidateIdsRemainStableForSameSourceText(){List<OcrPage> pages=[new(1,"INVOICE NO ABC123\n01.01.2026\nTOTAL 121.00 RON")];var a=ReceiptParser.Parse(pages,null);var b=ReceiptParser.Parse(pages,null);Assert.Equal(Assert.Single(a.Candidates).Id,Assert.Single(b.Candidates).Id);}
 [Fact]public void BlankAndCoverPagesRemainExplicitlyUnassigned(){var r=ReceiptParser.Parse([new(1,"Cover letter\nSupporting documents"),new(2,""),new(3,"INVOICE NO ABC123\nTOTAL 121.00 RON")],null);Assert.Equal(new[]{1,2},r.UnassignedPages);Assert.Single(r.Candidates);Assert.Equal(3,r.Candidates[0].Pages[0]);}
 [Fact]public void GrossAndRateDoNotFabricatePrintedVat(){var r=ReceiptParser.Parse([new(1,"INVOICE NO ABC123\nTOTAL 121.00 RON\n21% 121.00")],null);var c=Assert.Single(r.Candidates);Assert.Empty(c.Taxes);Assert.Contains(c.Warnings,w=>w.Contains("VAT_AMOUNT_NOT_PRINTED"));Assert.Equal("121.00",c.Fields["amount"].Value);}
 [Theory][InlineData("1.234,56")][InlineData("1,234.56")][InlineData("1\u00a0234,56")][InlineData("1\u202f234,56")]public void LocalizedAmountsPreserveValue(string text)=>Assert.Equal(1234.56m,ReceiptParser.Number(text));
 [Theory][InlineData("RON")][InlineData("BGN")][InlineData("CHF")][InlineData("CZK")][InlineData("DKK")][InlineData("EUR")][InlineData("GBP")][InlineData("HUF")][InlineData("NOK")][InlineData("PLN")][InlineData("SEK")][InlineData("USD")]public void WorkbookCurrencyIsNotSilentlyChanged(string currency){var r=ReceiptParser.Parse([new(1,"INVOICE NO ABC123\nTOTAL 121.00 "+currency)],null);Assert.Equal(currency,Assert.Single(r.Candidates).Fields["currency"].Value);}
}
