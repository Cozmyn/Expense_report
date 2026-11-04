using System.Text.RegularExpressions;using System.Globalization;using System.Security.Cryptography;using System.Text;
namespace Decont;
public static class DocumentSegmentation {
 static (int Current,int Total)? Numbering(OcrPage page){var m=Regex.Match(page.Text,@"(?i)\b(?:PAGE|PAGIN[ĂA]|PAG\.?)\s*(\d+)\s*(?:OF|DIN|/)\s*(\d+)\b");return m.Success&&int.TryParse(m.Groups[1].Value,out var current)&&int.TryParse(m.Groups[2].Value,out var total)?(current,total):null;}
 public static List<SourceRegion> RegionsFor(OcrPage page,int offset,int count,bool multiple){
  if(!multiple||page.Words==null)return[new(page.Number)];var words=page.Words.Skip(offset).Take(count).ToList();if(words.Count==0)return[new(page.Number)];var x=Math.Max(0,words.Min(w=>w.Box.X)-.01m);var y=Math.Max(0,words.Min(w=>w.Box.Y)-.01m);var right=Math.Min(1,words.Max(w=>w.Box.X+w.Box.Width)+.01m);var bottom=Math.Min(1,words.Max(w=>w.Box.Y+w.Box.Height)+.01m);return right>x&&bottom>y?[new(page.Number,x,y,right-x,bottom-y)]:[new(page.Number)];
 }
 public static List<OcrCandidate> Group(List<OcrPage> pages,List<OcrCandidate> candidates){
  var result=candidates.ToList();foreach(var page in pages.OrderBy(p=>p.Number)){var first=Numbering(page);if(first==null||first.Value.Current!=1||first.Value.Total<2||first.Value.Total>50)continue;
   var range=Enumerable.Range(page.Number,first.Value.Total).ToArray();var sequence=range.Select(n=>pages.SingleOrDefault(p=>p.Number==n)).ToList();if(sequence.Any(p=>p==null))continue;bool continuous=true;for(int i=0;i<sequence.Count;i++){var marker=Numbering(sequence[i]!);if(marker==null||marker.Value.Current!=i+1||marker.Value.Total!=first.Value.Total)continuous=false;}if(!continuous)continue;
   var members=result.Where(c=>c.Pages.Any(p=>range.Contains(p))).OrderBy(c=>c.Pages.Min()).ToList();if(members.Count==0||!members.Any(c=>c.Fields.GetValueOrDefault("documentType")?.Value=="Invoice"))continue;
   bool conflict=new[]{"invoice","cui","date"}.Any(key=>members.Select(c=>c.Fields.GetValueOrDefault(key)?.Value).Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1);
   if(conflict){foreach(var c in members)c.Warnings.Add("AMBIGUOUS_BOUNDARY: Numerele de pagină coincid, dar datele documentelor diferă. Verifică gruparea. / Page numbering matches but document identifiers differ. Review grouping.");continue;}
   var fields=new Dictionary<string,OcrField>();Dictionary<string,List<OcrField>> alternatives=[];foreach(var c in members)foreach(var pair in c.Fields){if(!alternatives.TryGetValue(pair.Key,out var list))alternatives[pair.Key]=list=[];list.Add(pair.Value);if(!fields.ContainsKey(pair.Key)||pair.Key=="amount")fields[pair.Key]=pair.Value;}
   var taxAlternatives=members.SelectMany(c=>c.Taxes).ToList();var taxes=taxAlternatives;var taxPages=taxes.Select(t=>t.Page).Distinct().ToList();var warnings=members.SelectMany(c=>c.Warnings).Distinct().ToList();
   if(taxPages.Count>1){var last=taxes.Where(t=>t.Page==taxPages.Max()).ToList();var gross=fields.GetValueOrDefault("amount")?.Value;var matchesGross=decimal.TryParse(gross,NumberStyles.Number,CultureInfo.InvariantCulture,out var total)&&Math.Abs(last.Sum(t=>t.Base+t.Vat)-total)<=.01m;var coversRates=taxes.Select(t=>t.Rate).Distinct().All(rate=>last.Any(t=>t.Rate==rate));if(matchesGross&&coversRates)taxes=last;else{taxes=[];warnings.Add("VAT_SUMMARY_REVIEW: Există mai multe defalcări TVA. Copiază sumarul final; valorile originale se păstrează. / Multiple VAT breakdowns require manual review of the final invoice summary; originals are retained.");}}
   warnings.Add("MULTIPAGE_REVIEW: Grupare propusă pe baza numerotării consecutive a paginilor. Confirmă originalele. / Proposed grouping uses consecutive page numbering. Confirm the originals.");
   List<SourceRegion> regions=[];foreach(var number in range){var found=members.Where(c=>c.Pages.Contains(number)).SelectMany(c=>c.Regions??c.Pages.Select(p=>new SourceRegion(p)).ToList()).Where(r=>r.Page==number).Distinct().ToList();regions.AddRange(found.Count>0?found:[new(number)]);}
   var hash=SHA256.HashData(Encoding.UTF8.GetBytes("numbered-v1|"+string.Join(",",range)+"|"+string.Join(",",members.Select(c=>c.Id))));var combined=new OcrCandidate(new Guid(hash.AsSpan(0,16)),range,fields,taxes,warnings,regions,"Consecutive printed page numbering; human verification required",null,alternatives,taxAlternatives);
   result.RemoveAll(c=>members.Contains(c));result.Add(combined);
  }return result.OrderBy(c=>c.Pages.Min()).ThenBy(c=>c.Regions?.FirstOrDefault()?.Y??0).ToList();
 }
}
