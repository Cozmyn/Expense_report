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
 public static Dictionary<string,string> FieldStates(ExpenseData d,IReadOnlyList<Master> masters){
  var fields=new Dictionary<string,string>{{"supplier","REQUIRED"},{"invoice","REQUIRED"},{"date","REQUIRED"},{"country","REQUIRED"},{"documentType","REQUIRED"},{"category","REQUIRED"},{"description","REQUIRED"},{"payment","REQUIRED"},{"costCenter","REQUIRED"},{"amount","REQUIRED"},{"currency","REQUIRED"},{"cui",d.Country=="RO"&&d.DocumentType=="InvoiceWithCui"?"REQUIRED":"NOT_APPLICABLE"},{"cardId",d.Payment=="CompanyCard"?"REQUIRED":"NOT_APPLICABLE"},{"project","OPTIONAL"},{"taxes",d.Country=="RO"?"REQUIRED":"NOT_APPLICABLE"}};
  if(!DateOnly.TryParse(d.Date,out var date))return fields;
  var values=new Dictionary<string,string>{{"country",d.Country},{"documentType",d.DocumentType},{"category",d.Category},{"payment",d.Payment},{"currency",d.Currency}};
  foreach(var m in masters.Where(m=>m.Kind=="fieldRules"&&Effective(m,date)).OrderBy(m=>Number(m,"priority"))){var json=Data(m);if(json.TryGetProperty("conditions",out var conditions)&&conditions.EnumerateObject().All(c=>values.GetValueOrDefault(c.Name)==c.Value.GetString())){var field=Text(m,"field");var state=Text(m,"state");if(fields.ContainsKey(field)&&state is "REQUIRED" or "OPTIONAL" or "NOT_APPLICABLE")fields[field]=state;}}
  return fields;
 }
 public static List<Problem> Validate(Report r,IReadOnlyList<Master> masters,bool erp=false){
  List<Problem> issues=[];var active=r.Lines.Where(l=>!l.Archived).ToList();
  if(string.IsNullOrWhiteSpace(r.Purpose))issues.Add(new("purpose","Scopul este obligatoriu.","Business purpose is required."));
  if(active.Count is <1 or >MaxLines)issues.Add(new("line_count","Adaugă între 1 și 50 de cheltuieli.","Add between 1 and 50 expenses."));
  if(string.IsNullOrWhiteSpace(r.ManagerId))issues.Add(new("manager","Configurează managerul pentru aprobare.","Configure the approval manager."));
  foreach(var l in active){var d=l.Data();void Add(string code,string ro,string en)=>issues.Add(new(code,ro,en,l.Id));
   if(!DateOnly.TryParse(d.Date,out var date)||date>DateOnly.FromDateTime(DateTime.UtcNow)){Add("date","Data documentului nu este validă.","Invalid document date.");continue;}
   var fields=FieldStates(d,masters);var data=JsonSerializer.SerializeToElement(d,Serialization.Options);
   foreach(var p in fields.Where(p=>p.Value=="REQUIRED")){if(!data.TryGetProperty(p.Key,out var v)||v.ValueKind is JsonValueKind.Null||v.ValueKind==JsonValueKind.String&&string.IsNullOrWhiteSpace(v.GetString())||v.ValueKind==JsonValueKind.Array&&v.GetArrayLength()==0)Add("required",$"Completează câmpul {p.Key}.",$"Complete {p.Key}.");}
   if(d.Amount<=0)Add("amount","Suma trebuie să fie pozitivă.","The amount must be positive.");
   if(d.Payment is not ("Personal" or "CompanyCard"))Add("payment","Metoda de plată nu este validă.","Invalid payment method.");
   if(d.DocumentType is not ("InvoiceWithCui" or "InvoiceWithoutCui" or "Receipt"))Add("document_type","Tipul documentului nu este valid.","Invalid document type.");
   if(d.Currency.Length!=3||!d.Currency.All(char.IsAsciiLetterUpper)||d.Country.Length!=2||!d.Country.All(char.IsAsciiLetterUpper))Add("codes","Codul țării sau monedei nu este valid.","Invalid country or currency code.");
   if(fields["cui"]=="REQUIRED"&&!ValidCui(d.Cui))Add("cui","CUI-ul nu este valid.","Invalid Romanian VAT ID.");
   if(d.Payment=="Personal"&&d.CardId!=null)Add("card_na","Cardul nu se aplică plății personale.","A company card does not apply to personal payment.");
   if(d.Payment=="CompanyCard"&&!masters.Any(m=>m.Kind=="cards"&&m.Id==d.CardId&&Effective(m,date)))Add("card","Selectează un card activ.","Select an active card.");
   foreach(var pair in new[]{("costCenters",d.CostCenter,"cost_center"),("expenseTypes",d.Category,"category")})if(!masters.Any(m=>m.Kind==pair.Item1&&m.Code==pair.Item2&&Effective(m,date)))Add(pair.Item3,"Nomenclatorul nu este valid la data documentului.","The master value is invalid for the document date.");
   if(!string.IsNullOrEmpty(d.Project)&&!masters.Any(m=>m.Kind=="projects"&&m.Code==d.Project&&Effective(m,date)))Add("project","Proiectul nu este valid.","Invalid project.");
   if(d.Currency!="RON"&&l.FxVersion==null)Add("fx","Lipsește cursul intern.","Internal FX rate is missing.");
   if(!l.Evidence.Any(e=>!e.Archived)||l.Evidence.Any(e=>!e.Archived&&e.Scan!="Clean"))Add("evidence","Documentele trebuie încărcate și verificate.","Upload documents and wait for the security scan.");
   if(d.Country=="RO"){
    if(d.Taxes.Select(t=>t.RateId).Distinct().Count()!=d.Taxes.Count)Add("vat_duplicate","Cota TVA este duplicată.","Duplicate VAT rate.");
    foreach(var t in d.Taxes){var rate=masters.SingleOrDefault(m=>m.Kind=="vat"&&m.Id==t.RateId&&Effective(m,date));if(rate==null||Number(rate,"rate")==5){Add("vat_rate","Cota TVA nu este activă.","VAT rate is inactive.");continue;}if(t.Base<0||t.Vat<0||Math.Abs(Money(t.Base*Number(rate,"rate")/100)-t.Vat)>1)Add("vat_arithmetic","TVA diferă cu mai mult de 1 RON.","VAT differs by more than 1 RON.");}
    if(Math.Abs(d.Taxes.Sum(t=>t.Base+t.Vat)-l.Final())>1)Add("vat_total","Defalcarea TVA diferă de total.","VAT breakdown differs from the total.");
   }else if(d.Taxes.Count>0)Add("vat_na","TVA domestic nu se aplică.","Domestic VAT does not apply.");
   if(erp){var mapping=masters.SingleOrDefault(m=>m.Kind=="accounting"&&m.Code==d.Category&&Effective(m,date));if(mapping==null||Text(mapping,"mainAccount")==""||Text(mapping,"localAccount")=="")Add("mapping","Lipsește maparea GL / cont local.","GL / local account mapping is missing.");
    if(d.Country=="RO"&&d.DocumentType=="InvoiceWithCui"){var supplier=masters.SingleOrDefault(m=>m.Kind=="suppliers"&&m.Code==Cui(d.Cui)&&Effective(m,date));if(supplier==null||Text(supplier,"vendorAccount")=="")Add("vendor","Lipsește contul ERP al furnizorului.","ERP vendor account is missing.");}
    if(d.Payment=="CompanyCard"&&!l.Reconciled&&string.IsNullOrWhiteSpace(l.OverrideReason))Add("reconciliation","Cardul necesită reconciliere.","Card reconciliation is required.");
   }
  }return issues;
 }
}
