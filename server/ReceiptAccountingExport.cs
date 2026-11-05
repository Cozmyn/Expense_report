using ClosedXML.Excel;
namespace Decont;
public static class ReceiptAccountingExport {
 public static byte[] Generate(Report report,List<Master> masters){
  using var workbook=new XLWorkbook();var receipts=workbook.AddWorksheet("Bonuri");var vat=workbook.AddWorksheet("TVA pe cote");var calculations=workbook.AddWorksheet("Calcule Finance");Header(calculations,["Bon / Receipt","Estimare RON / Estimate RON","Final RON","Rotunjire RON / Rounding RON","ID calcul / Calculation ID","Hash intrare / Input hash","ID decizie FX / FX decision ID","Bază sursă / Source base","Bază confirmată / Confirmed base"]);int calculationRow=2;
  string[] receiptHeaders=["Decont / Report","Revizie / Revision","Versiune / Version","Bon / Receipt","ID bon / Receipt ID","Data / Date","Tip cheltuială / Category","Descriere / Description","Furnizor RO / Romanian supplier","CUI companie / Company VAT ID","CUI furnizor / Supplier VAT ID","Furnizor / Supplier","Număr bon / Receipt number","Monedă / Currency","Total bon / Receipt total","Final RON","Plată / Payment","Centru cost / Cost centre","Proiect / Project","Fișiere / Files"];
  string[] vatHeaders=["Decont / Report","Revizie / Revision","Versiune / Version","Bon / Receipt","ID bon / Receipt ID","CUI furnizor / Supplier VAT ID","Cotă TVA / VAT rate (%)","Monedă TVA / VAT currency","Bază fără TVA / Base excluding VAT","TVA plătit / VAT paid","Bază + TVA / Base + VAT","ID mapare TVA / VAT mapping ID"];
  Header(receipts,receiptHeaders);Header(vat,vatHeaders);int receiptRow=2,vatRow=2;
  foreach(var line in report.Lines.Where(l=>!l.Archived).OrderBy(l=>l.Position)){var d=line.Data();var taxable=Rules.DomesticVat(d);var files=line.Evidence.Where(e=>!e.Archived).Select(e=>e.Name).Concat(report.Attachments.Where(e=>!e.Archived&&e.Covers(line.Id)).Select(e=>e.Name));
   object?[] values=[report.Number,report.Revision,report.Version,line.Position,line.Id.ToString(),d.Date,d.Category,d.Description,d.Country=="RO"?"Da / Yes":"Nu / No",taxable?"Da / Yes":"Nu / No",d.Cui,d.Supplier,d.Invoice,d.Currency,d.Amount,line.Final(),d.Payment,d.CostCenter,d.Project,string.Join("; ",files)];Write(receipts,receiptRow++,values);
   var confirmed=line.CalculatedJson!=null&&line.CalculationHash==FinancialCalculations.Stamp(line)?Serialization.Read<ExpenseData>(line.CalculatedJson):null;Write(calculations,calculationRow++,[line.Position,line.EstimatedRon,line.Final(),line.RoundingRon,line.CalculationId?.ToString(),line.CalculationHash,line.FxDecisionId?.ToString(),d.Taxes.Sum(t=>t.Base),confirmed?.Taxes.Sum(t=>t.Base)]);
   if(taxable)foreach(var tax in d.Taxes){var mapping=masters.FirstOrDefault(m=>m.Id==tax.RateId);Write(vat,vatRow++,[report.Number,report.Revision,report.Version,line.Position,line.Id.ToString(),d.Cui,mapping==null?null:Rules.Number(mapping,"rate"),d.TaxCurrency,tax.Base,tax.Vat,Rules.Money(tax.Base+tax.Vat),tax.RateId.ToString()]);}
  }
  foreach(var sheet in workbook.Worksheets){sheet.SheetView.FreezeRows(1);sheet.RangeUsed()?.SetAutoFilter();sheet.Columns().AdjustToContents();foreach(var col in sheet.ColumnsUsed())if(col.Width>60)col.Width=60;}
  receipts.Columns(15,16).Style.NumberFormat.Format="0.00";vat.Columns(9,11).Style.NumberFormat.Format="0.00";
  using var stream=new MemoryStream();workbook.SaveAs(stream);return stream.ToArray();
 }
 static void Header(IXLWorksheet sheet,string[] headers){for(int i=0;i<headers.Length;i++)sheet.Cell(1,i+1).Value=headers[i];sheet.Row(1).Style.Font.Bold=true;}
 static void Write(IXLWorksheet sheet,int row,object?[] values){for(int i=0;i<values.Length;i++){var cell=sheet.Cell(row,i+1);if(values[i] is decimal amount)cell.Value=amount;else if(values[i] is int number)cell.Value=number;else if(values[i] is long version)cell.Value=version;else cell.Value=Convert.ToString(values[i],System.Globalization.CultureInfo.InvariantCulture)??"";}}
}

