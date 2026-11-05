using System.Globalization;using System.Text;using System.Text.Json;using System.Text.RegularExpressions;
namespace Decont;
public record CategorySuggestion(string Code,string NameRo,string NameEn,string[] MatchedTerms);
public record AllocationDefaults(string? CostCenter,string? Project,string Source);
public static class DocumentAssistance {
 static string Fold(string text)=>string.Concat(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
 public static List<CategorySuggestion> Categories(string text,IEnumerable<Master> masters,DateOnly date){
  var evidence=Fold(text);List<CategorySuggestion> result=[];
  foreach(var master in masters.Where(m=>m.Kind=="expenseTypes"&&Rules.Effective(m,date))){using var json=JsonDocument.Parse(master.Json);var terms=new List<string>{master.NameRo,master.NameEn};if(json.RootElement.TryGetProperty("ocrKeywords",out var words)&&words.ValueKind==JsonValueKind.Array)terms.AddRange(words.EnumerateArray().Where(w=>w.ValueKind==JsonValueKind.String).Select(w=>w.GetString()!));var found=terms.Where(w=>w.Length is >=3 and <=80).Distinct(StringComparer.OrdinalIgnoreCase).Where(w=>Regex.IsMatch(evidence,@"(?<![\p{L}\p{Nd}])"+Regex.Escape(Fold(w))+@"(?![\p{L}\p{Nd}])")).Take(8).ToArray();if(found.Length>0)result.Add(new(master.Code,master.NameRo,master.NameEn,found));}
  return result.OrderByDescending(c=>c.MatchedTerms.Length).ThenBy(c=>c.Code).Take(5).ToList();
 }
 public static AllocationDefaults Defaults(IEnumerable<Master> masters,Report report,DateOnly date){
  var all=masters.ToList();var policy=all.FirstOrDefault(m=>m.Kind=="policies"&&m.Code=="expenseDefaults"&&Rules.Effective(m,date));if(policy==null)return new(null,null,"None");using var json=JsonDocument.Parse(policy.Json);var value=json.RootElement;var source="Company";
  if(value.TryGetProperty("workers",out var workers)&&workers.ValueKind==JsonValueKind.Object&&!string.IsNullOrEmpty(report.EmployeeWorker)&&workers.TryGetProperty(report.EmployeeWorker,out var worker)&&worker.ValueKind==JsonValueKind.Object){value=worker;source="Employee";}
  else if(value.TryGetProperty("departments",out var departments)&&departments.ValueKind==JsonValueKind.Object&&!string.IsNullOrEmpty(report.DepartmentCode)&&departments.TryGetProperty(report.DepartmentCode,out var department)&&department.ValueKind==JsonValueKind.Object){value=department;source="Department";}
  string? Valid(string field,string kind){if(!value.TryGetProperty(field,out var item)||item.ValueKind!=JsonValueKind.String)return null;var code=item.GetString();return all.Any(m=>m.Kind==kind&&m.Code==code&&Rules.Effective(m,date))?code:null;}
  return new(Valid("costCenter","costCenters"),Valid("project","projects"),source);
 }
 public static string Evidence(OcrResult result,List<SourceRegion> regions){
  List<string> text=[];foreach(var region in regions){var page=result.Pages.FirstOrDefault(p=>p.Number==region.Page);if(page==null)continue;if(page.Words?.Count>0)text.Add(string.Join(" ",page.Words.Where(w=>w.Box.X+w.Box.Width/2>=region.X&&w.Box.X+w.Box.Width/2<=region.X+region.Width&&w.Box.Y+w.Box.Height/2>=region.Y&&w.Box.Y+w.Box.Height/2<=region.Y+region.Height).Select(w=>w.Text)));else if(region.X==0&&region.Y==0&&region.Width==1&&region.Height==1)text.Add(page.Text);}
  var combined=string.Join("\n",text);return combined[..Math.Min(combined.Length,12000)];
 }
}
