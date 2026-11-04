using System.Text.Json;
namespace Decont;
public static class Rules {
 public const int MaxLines=50;public static decimal Money(decimal x)=>decimal.Round(x,2,MidpointRounding.AwayFromZero);
 public static string Cui(string? x){var s=(x??"").Trim().ToUpperInvariant().Replace(" ","");return s.StartsWith("RO")?s[2..]:s;}
 public static bool ValidCui(string? value){var s=Cui(value);if(s.Length is <2 or >10||s.Any(c=>!char.IsAsciiDigit(c)))return false;var p=s.PadLeft(10,'0');var sum=0;for(var i=0;i<9;i++)sum+=(p[i]-'0')*("753217532"[i]-'0');var check=sum*10%11;return (check==10?0:check)==p[9]-'0';}
 public static bool Effective(Master m,DateOnly date)=>m.Active&&m.From<=date&&(m.To==null||m.To>=date);
 public static JsonElement Data(Master m)=>JsonDocument.Parse(m.Json).RootElement.Clone();
 public static string Text(Master m,string key)=>Data(m).TryGetProperty(key,out var v)?v.GetString()??"":"";
 public static decimal Number(Master m,string key)=>Data(m).TryGetProperty(key,out var v)?v.GetDecimal():0;
 public static decimal Personal(Report r)=>Money(r.Lines.Where(l=>!l.Archived&&l.Data().Payment=="Personal").Sum(l=>l.Final()));
 public static bool DomesticVat(ExpenseData d)=>d.Country=="RO"&&(d.HasCompanyCui??d.DocumentType=="InvoiceWithCui");
 public static int VatCodeCount(ExpenseData d,IReadOnlyList<Master> masters,decimal? ronFactor=null){var day=DateOnly.Parse(d.Date);var policy=masters.FirstOrDefault(m=>m.Kind=="policies"&&m.Code=="vat"&&Effective(m,day));var tolerance=policy!=null&&Data(policy).TryGetProperty("vatZeroTolerance",out var zero)?zero.GetDecimal():2;var fx=ronFactor??(d.TaxCurrency=="RON"?1:masters.Where(m=>m.Kind=="fx"&&m.Code==d.TaxCurrency&&Effective(m,day)).Select(m=>Number(m,"ronRate")).FirstOrDefault());Guard.Require(fx>0,"fx","Lipsește cursul pentru verificarea TVA.","FX rate is missing for VAT validation.");return d.Taxes.Count(t=>{var m=masters.FirstOrDefault(m=>m.Id==t.RateId);return m!=null&&(Number(m,"rate")!=0||Math.Abs(t.Base*fx)>tolerance);});}
 public static decimal TaxTotal(ExpenseData d)=>Money(d.Taxes.Sum(t=>t.Base+t.Vat));
 public static decimal TaxExpected(Line l)=>l.Data().TaxCurrency==l.Data().Currency?l.Data().Amount:l.EstimatedRon;
 public static List<Problem> VatIssues(Line l,IReadOnlyList<Master> masters){
  var d=l.Data();List<Problem> issues=[];void Add(string code,string ro,string en)=>issues.Add(new(code,ro,en,l.Id));
  if(!DomesticVat(d)){if(d.Taxes.Count>0)Add("vat_na","TVA nu se completează pentru acest bon.","VAT is not applicable to this receipt.");return issues;}
  if(d.TaxCurrency!=d.Currency&&d.TaxCurrency!="RON")Add("vat_currency","Moneda defalcării TVA este invalidă.","Invalid VAT breakdown currency.");
  if(!DateOnly.TryParse(d.Date,out var date))return issues;
  if(d.Amount<=0||l.EstimatedRon<=0){Add("amount","Suma și valoarea în RON trebuie să fie pozitive.","Amount and RON value must be positive.");return issues;}
  if(d.Taxes.Count==0)Add("vat_missing","Completează baza și TVA pentru cotele de pe bon: 0%, 11%, 21%.","Enter taxable base and VAT for the receipt rates: 0%, 11%, 21%.");
  var rates=new HashSet<decimal>();
  foreach(var t in d.Taxes){var rate=masters.SingleOrDefault(m=>m.Kind=="vat"&&m.Id==t.RateId&&Effective(m,date));if(rate==null||Number(rate,"rate")<0||Number(rate,"rate")>100||Number(rate,"rate")==5){Add("vat_rate","Cota TVA nu este activă la data bonului.","VAT rate is not active for the receipt date.");continue;}
   var pct=Number(rate,"rate");if(!rates.Add(pct))Add("vat_duplicate","Aceeași cotă TVA este completată de două ori.","The same VAT percentage is entered twice.");
   if(t.Base<0||t.Vat<0||t.Base!=Money(t.Base)||t.Vat!=Money(t.Vat)||pct==0&&t.Vat!=0||Math.Abs(Money(t.Base*pct/100)-t.Vat)*(d.TaxCurrency=="RON"?1:l.EstimatedRon/d.Amount)>1)Add("vat_arithmetic",$"Verifică baza și TVA la {pct}%: TVA așteptat {Money(t.Base*pct/100):0.00}, introdus {t.Vat:0.00}.",$"Check base and VAT at {pct}%: expected {Money(t.Base*pct/100):0.00}, entered {t.Vat:0.00}.");
  }
  if(Math.Abs(TaxTotal(d)-TaxExpected(l))>0.01m)Add("vat_total",$"Totalul recompus din baze + TVA ({TaxTotal(d):0.00} {d.TaxCurrency}) diferă de totalul bonului ({TaxExpected(l):0.00}).",$"Reconstructed base + VAT total ({TaxTotal(d):0.00} {d.TaxCurrency}) differs from the receipt total ({TaxExpected(l):0.00}).");
  return l.VatOverrideReason==null?issues:issues.Select(p=>p.Code is "vat_arithmetic" or "vat_total"?p with{Blocking=false}:p).ToList();
 }
 public static Dictionary<string,string> FieldStates(ExpenseData d,IReadOnlyList<Master> masters){
  var fields=new Dictionary<string,string>{{"supplier",DomesticVat(d)?"REQUIRED":"NOT_APPLICABLE"},{"invoice",DomesticVat(d)?"REQUIRED":"OPTIONAL"},{"date","REQUIRED"},{"country","REQUIRED"},{"documentType","REQUIRED"},{"category","REQUIRED"},{"description","REQUIRED"},{"payment","REQUIRED"},{"costCenter","REQUIRED"},{"amount","REQUIRED"},{"currency","REQUIRED"},{"cui",DomesticVat(d)?"REQUIRED":"NOT_APPLICABLE"},{"cardId",d.Payment=="CompanyCard"?"REQUIRED":"NOT_APPLICABLE"},{"project","OPTIONAL"},{"taxes",DomesticVat(d)?"REQUIRED":"NOT_APPLICABLE"}};
  if(!DateOnly.TryParse(d.Date,out var date))return fields;
  var values=new Dictionary<string,string>{{"country",d.Country},{"documentType",d.DocumentType},{"category",d.Category},{"payment",d.Payment},{"currency",d.Currency}};
  foreach(var m in masters.Where(m=>m.Kind=="fieldRules"&&Effective(m,date)).OrderBy(m=>Number(m,"priority"))){var json=Data(m);if(json.TryGetProperty("conditions",out var conditions)&&conditions.EnumerateObject().All(c=>values.GetValueOrDefault(c.Name)==c.Value.GetString())){var field=Text(m,"field");var state=Text(m,"state");if(fields.ContainsKey(field)&&state is "REQUIRED" or "OPTIONAL" or "NOT_APPLICABLE")fields[field]=state;}}
  if(DomesticVat(d)){fields["cui"]="REQUIRED";fields["taxes"]="REQUIRED";fields["supplier"]="REQUIRED";fields["invoice"]="REQUIRED";}else{fields["cui"]="NOT_APPLICABLE";fields["taxes"]="NOT_APPLICABLE";fields["supplier"]="NOT_APPLICABLE";fields["invoice"]="OPTIONAL";}
  foreach(var core in new[]{"date","country","documentType","category","description","payment","amount","currency"})fields[core]="REQUIRED";fields["cardId"]=d.Payment=="CompanyCard"?"REQUIRED":"NOT_APPLICABLE";return fields;
 }
 public static List<Problem> Validate(Report r,IReadOnlyList<Master> masters,bool erp=false,IReadOnlyList<SupplierAccount>? supplierAccounts=null){
  List<Problem> issues=[];var active=r.Lines.Where(l=>!l.Archived).ToList();
  if(active.Select(l=>l.Data().Payment).Distinct().Count()>1)issues.Add(new("mixed_payment","Folosește deconturi separate pentru plata personală și cardul companiei.","Use separate reports for personal payments and company-card expenses."));
  if(string.IsNullOrWhiteSpace(r.Purpose))issues.Add(new("purpose","Scopul este obligatoriu.","Business purpose is required."));
  if(active.Count is <1 or >MaxLines)issues.Add(new("line_count","Adaugă între 1 și 50 de cheltuieli.","Add between 1 and 50 expenses."));
  if(string.IsNullOrWhiteSpace(r.ManagerId))issues.Add(new("manager","Configurează managerul pentru aprobare.","Configure the approval manager."));
  var workflow=masters.FirstOrDefault(m=>m.Kind=="policies"&&m.Code=="workflow"&&Effective(m,DateOnly.FromDateTime(DateTime.UtcNow)));var requireCard=workflow!=null&&Data(workflow).TryGetProperty("requireCardMatchBeforeApproval",out var requiredCard)&&requiredCard.GetBoolean();
  foreach(var l in active){var d=l.Data();void Add(string code,string ro,string en)=>issues.Add(new(code,ro,en,l.Id));
   if(!DateOnly.TryParse(d.Date,out var date)||date>DateOnly.FromDateTime(DateTime.UtcNow)){Add("date","Data documentului nu este validă.","Invalid document date.");continue;}
   var fields=FieldStates(d,masters);var data=JsonSerializer.SerializeToElement(d,Serialization.Options);
   var fieldNames=new Dictionary<string,(string Ro,string En)>{{"supplier",("denumire furnizor","supplier name")},{"invoice",("număr bon/document","document number")},{"date",("data documentului","document date")},{"country",("țară","country")},{"documentType",("tip document","document type")},{"category",("tip cheltuială","expense category")},{"description",("descriere","description")},{"payment",("metodă de plată","payment method")},{"costCenter",("centru de cost","cost centre")},{"amount",("sumă","amount")},{"currency",("monedă","currency")},{"cui",("CUI furnizor","supplier VAT ID")},{"cardId",("card companie","company card")},{"project",("proiect","project")},{"taxes",("baze și TVA","taxable bases and VAT")}};foreach(var p in fields.Where(p=>p.Value=="REQUIRED")){if(!data.TryGetProperty(p.Key,out var v)||v.ValueKind is JsonValueKind.Null||v.ValueKind==JsonValueKind.String&&string.IsNullOrWhiteSpace(v.GetString())||v.ValueKind==JsonValueKind.Array&&v.GetArrayLength()==0)Add("required",$"Completează câmpul {fieldNames[p.Key].Ro}.",$"Complete {fieldNames[p.Key].En}.");}
   if(d.Amount<=0)Add("amount","Suma trebuie să fie pozitivă.","The amount must be positive.");
   if(d.Payment is not ("Personal" or "CompanyCard"))Add("payment","Metoda de plată nu este validă.","Invalid payment method.");
   if(d.DocumentType is not ("InvoiceWithCui" or "InvoiceWithoutCui" or "Receipt")&&!masters.Any(m=>m.Kind=="documentTypes"&&m.Code==d.DocumentType&&Effective(m,date)))Add("document_type","Tipul documentului nu este valid.","Invalid document type.");
   if(d.Currency.Length!=3||!d.Currency.All(char.IsAsciiLetterUpper)||d.Country.Length!=2||!d.Country.All(char.IsAsciiLetterUpper))Add("codes","Codul țării sau monedei nu este valid.","Invalid country or currency code.");
   if(fields["cui"]=="REQUIRED"&&!ValidCui(d.Cui))Add("cui","CUI-ul nu este valid.","Invalid Romanian VAT ID.");
   if(!erp&&requireCard&&(r.State is States.Manager or States.Reapproval)&&d.Payment=="CompanyCard"&&!l.Reconciled&&string.IsNullOrWhiteSpace(l.OverrideReason))Add("reconciliation","Politica solicită reconcilierea cardului înainte de aprobare.","Policy requires card reconciliation before approval.");
   if(d.Payment=="Personal"&&d.CardId!=null)Add("card_na","Cardul nu se aplică plății personale.","A company card does not apply to personal payment.");
   if(d.Payment=="CompanyCard"&&!masters.Any(m=>m.Kind=="cards"&&m.Id==d.CardId&&Effective(m,date)))Add("card","Selectează un card activ.","Select an active card.");
   foreach(var pair in new[]{("costCenters",d.CostCenter,"cost_center"),("expenseTypes",d.Category,"category")})if((pair.Item1!="costCenters"||fields["costCenter"]!="NOT_APPLICABLE"&&!(fields["costCenter"]=="OPTIONAL"&&string.IsNullOrEmpty(d.CostCenter)))&&!masters.Any(m=>m.Kind==pair.Item1&&m.Code==pair.Item2&&Effective(m,date)))Add(pair.Item3,"Nomenclatorul nu este valid la data documentului.","The master value is invalid for the document date.");
   if(fields["project"]!="NOT_APPLICABLE"&&!string.IsNullOrEmpty(d.Project)&&!masters.Any(m=>m.Kind=="projects"&&m.Code==d.Project&&Effective(m,date)))Add("project","Proiectul nu este valid.","Invalid project.");
   if(d.Currency!="RON"&&l.FxVersion==null)Add("fx","Lipsește cursul intern.","Internal FX rate is missing.");
   var files=l.Evidence.Where(e=>!e.Archived).Cast<IOriginalDocument>().Concat(r.Attachments.Where(e=>!e.Archived&&e.Covers(l.Id))).ToList();
   if(files.Count==0)Add("evidence","Încarcă fișierul acestui bon sau un fișier comun care îl include.","Upload this receipt or a combined file covering it.");
   else if(files.Any(e=>e.Scan!="Clean"))Add("evidence_scan","Așteaptă scanarea documentelor; fișierele respinse trebuie înlocuite sau arhivate.","Wait for document scans; replace or archive rejected files.");
   if(d.Country=="RO"&&d.HasCompanyCui==null&&d.DocumentType is not ("InvoiceWithCui" or "InvoiceWithoutCui"))Add("company_cui","Precizează dacă bonul are CUI-ul companiei.","Specify whether the receipt includes the company VAT ID.");
   if(DomesticVat(d)){var supplier=masters.SingleOrDefault(m=>m.Kind=="suppliers"&&m.Code==Cui(d.Cui)&&Effective(m,date));if(supplier==null&&!d.SupplierConfirmed)Add("supplier_confirmation","CUI-ul nu există în baza internă. Verifică-l pe bon și confirmă că este corect; Finance va adăuga furnizorul.","VAT ID is absent from the internal database. Check the receipt and confirm it is correct; Finance will add the supplier.");}
   issues.AddRange(VatIssues(l,masters));
   if(erp){var mapping=masters.SingleOrDefault(m=>m.Kind=="accounting"&&m.Code==d.Category&&Effective(m,date));if(mapping==null||d.Accounting==null&&(Text(mapping,"mainAccount")==""||Text(mapping,"localAccount")==""))Add("mapping","Lipsește maparea GL / cont local.","GL / local account mapping is missing.");
    if(DomesticVat(d)){var supplier=masters.SingleOrDefault(m=>m.Kind=="suppliers"&&m.Code==Cui(d.Cui)&&Effective(m,date));if(supplier==null||Text(supplier,"vendorAccount")==""&&!(supplierAccounts??[]).Any(a=>a.Cui==Cui(d.Cui)&&a.From<=date&&(a.To==null||a.To>=date)))Add("vendor","Lipsește contul ERP al furnizorului.","ERP vendor account is missing.");}
    if(d.Payment=="CompanyCard"&&!l.Reconciled&&string.IsNullOrWhiteSpace(l.OverrideReason))Add("reconciliation","Cardul necesită reconciliere.","Card reconciliation is required.");
   }
  }return issues;
 }
}
