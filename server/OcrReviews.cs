using Microsoft.EntityFrameworkCore;
namespace Decont;

public class DocumentReview {
 public Guid Id {get;set;}public Guid ReportId {get;set;}public string OriginalExtraction {get;set;}="";public int PageCount {get;set;}public string Dispositions {get;set;}="[]";public DateTime Created {get;set;}=DateTime.UtcNow;
}
public class DocumentGroup {
 public Guid Id {get;set;}=Guid.NewGuid();public Guid BundleId {get;set;}public string SourceKey {get;set;}="";public string State {get;set;}="ReviewRequired";public string Regions {get;set;}="[]";public string Fields {get;set;}="{}";public string Taxes {get;set;}="[]";public string Warnings {get;set;}="[]";public string Parents {get;set;}="[]";public Guid? ExpenseId {get;set;}public string? VerifiedBy {get;set;}public DateTime? VerifiedAt {get;set;}public string? CorrectedExpense {get;set;}
}
public class ExtractionAttempt {
 public Guid Id {get;set;}=Guid.NewGuid();public Guid BundleId {get;set;}public string Fingerprint {get;set;}="";public string Json {get;set;}="";public DateTime Created {get;set;}=DateTime.UtcNow;
}
public record SourceRegion(int Page,decimal X=0,decimal Y=0,decimal Width=1,decimal Height=1);
public record PageDisposition(int Page,string Reason);
public record GroupCommand(long Version,string Action,Guid[]? Groups=null,SourceRegion[]? Regions=null,int[]? Pages=null,string? Reason=null);
public record PageCommand(long Version,int Page,bool Supporting,string Reason);

public static class OcrReviews {
 public static bool Active(DocumentGroup g)=>g.State is "ReviewRequired" or "Verified";
 public static List<SourceRegion> Regions(DocumentGroup g)=>Serialization.Read<List<SourceRegion>>(g.Regions);
 public static bool Overlap(SourceRegion a,SourceRegion b)=>a.Page==b.Page&&a.X<b.X+b.Width&&b.X<a.X+a.Width&&a.Y<b.Y+b.Height&&b.Y<a.Y+a.Height;
 public static void ValidateRegions(IEnumerable<SourceRegion> regions,int pages){var all=regions.ToList();Guard.Require(all.Count>0&&all.Count<=100&&all.All(r=>r.Page>=1&&r.Page<=pages&&r.X>=0&&r.Y>=0&&r.Width>0&&r.Height>0&&r.X+r.Width<=1&&r.Y+r.Height<=1),"region","Selectează pagini și regiuni valide.","Select valid pages and regions.");}
 public static async Task Import(Db db,Guid report,Guid document,OcrResult result){
  if(result.Pages.Count==0)return;
  var json=Serialization.Write(result);var fingerprint=Serialization.Hash(json);
  var review=await db.DocumentReviews.FindAsync(document);
  if(review==null){review=new(){Id=document,ReportId=report,OriginalExtraction=json,PageCount=result.Pages.Max(p=>p.Number)};db.DocumentReviews.Add(review);
   foreach(var c in result.Candidates){db.DocumentGroups.Add(new(){BundleId=document,SourceKey=c.Id.ToString(),Regions=Serialization.Write(c.Pages.Select(p=>new SourceRegion(p)).ToList()),Fields=Serialization.Write(c.Fields),Taxes=Serialization.Write(c.Taxes),Warnings=Serialization.Write(c.Warnings)});}
  }
  // Raw attempts are append-only; retry never overwrites corrected/verified groups.
  if(!await db.ExtractionAttempts.AnyAsync(a=>a.BundleId==document&&a.Fingerprint==fingerprint))db.ExtractionAttempts.Add(new(){BundleId=document,Fingerprint=fingerprint,Json=json});
 }
 public static int[] Unassigned(DocumentReview review,IEnumerable<DocumentGroup> groups){
  var assigned=groups.Where(Active).SelectMany(Regions).Select(r=>r.Page).Concat(Serialization.Read<List<PageDisposition>>(review.Dispositions).Select(p=>p.Page)).ToHashSet();
  return Enumerable.Range(1,review.PageCount).Where(p=>!assigned.Contains(p)).ToArray();
 }
 public static async Task<object> View(Db db,DocumentReview review){
  var groups=await db.DocumentGroups.Where(g=>g.BundleId==review.Id).ToListAsync();
  return new{review.Id,review.PageCount,unassignedPages=Unassigned(review,groups),dispositions=Serialization.Read<List<PageDisposition>>(review.Dispositions),candidates=groups.Where(Active).Select(g=>new{g.Id,g.State,g.ExpenseId,correctedExpense=g.CorrectedExpense==null?null:Serialization.Read<ExpenseData>(g.CorrectedExpense),pages=Regions(g).Select(r=>r.Page).Distinct().Order().ToArray(),regions=Regions(g),fields=Serialization.Read<Dictionary<string,OcrField>>(g.Fields),taxes=Serialization.Read<List<OcrTax>>(g.Taxes),warnings=Serialization.Read<List<string>>(g.Warnings)}),historyCount=await db.Audits.CountAsync(a=>a.Entity==review.Id.ToString()),attempts=await db.ExtractionAttempts.CountAsync(a=>a.BundleId==review.Id)};
 }
 public static async Task<List<Problem>> Problems(Db db,Report report){
  var ids=report.Attachments.Where(a=>!a.Archived).Select(a=>a.Id).ToList();var reviews=await db.DocumentReviews.Where(r=>ids.Contains(r.Id)).ToListAsync();List<Problem> issues=[];
  foreach(var review in reviews){var groups=await db.DocumentGroups.Where(g=>g.BundleId==review.Id).ToListAsync();if(Unassigned(review,groups).Length>0)issues.Add(new("ocr_unassigned_pages","Verifică paginile neatribuite din documentele încărcate.","Review unassigned pages in uploaded documents."));
   if(groups.Where(Active).Any(g=>g.State!="Verified"||g.ExpenseId==null||!report.Lines.Any(l=>!l.Archived&&l.Id==g.ExpenseId)))issues.Add(new("ocr_unverified_groups","Verifică fiecare document OCR sau marchează-l justificativ cu motiv.","Verify each OCR document or mark it as supporting evidence with a reason."));
  }return issues;
 }
 public static async Task Verify(Db db,Reports svc,Report report,Line line,Guid document,Guid groupId,HttpContext context,string? reason){
  Guard.Require(report.Attachments.Any(a=>a.Id==document&&!a.Archived),"source","Document sursă indisponibil.","Source document unavailable.",404);var group=await db.DocumentGroups.SingleOrDefaultAsync(g=>g.Id==groupId&&g.BundleId==document);
  Guard.Require(group!=null&&Active(group),"ocr_group","Grupul sursă nu este disponibil.","Source group is unavailable.");
  Guard.Require(group!.ExpenseId==null||group.ExpenseId==line.Id,"ocr_consumed","Documentul a creat deja un bon. Deschide bonul existent.","This document already created a receipt. Open the existing receipt.",409);
  var review=await db.DocumentReviews.SingleAsync(r=>r.Id==document&&r.ReportId==report.Id);var source=report.Attachments.Single(a=>a.Id==document&&!a.Archived);
  Guard.Require(source.Scan=="Clean","scan","Așteaptă scanarea documentului.","Wait for document scanning.");
  var regions=Regions(group);ValidateRegions(regions,review.PageCount);
  var others=await db.DocumentGroups.Where(g=>g.BundleId==document&&g.Id!=group.Id&&g.State=="Verified").ToListAsync();
  Guard.Require(!others.Any(g=>Regions(g).Any(a=>regions.Any(b=>Overlap(a,b)))),"region_overlap","Regiunile se suprapun cu un bon verificat. Corectează delimitarea.","Regions overlap a verified receipt. Correct the boundaries.");
  var duplicateBundles=await db.ReportAttachments.Where(a=>a.Hash==source.Hash&&a.Id!=document&&!a.Archived).Select(a=>a.Id).ToListAsync();
  var duplicateGroups=await db.DocumentGroups.Where(g=>duplicateBundles.Contains(g.BundleId)&&g.State=="Verified").ToListAsync();
  Guard.Require(!duplicateGroups.Any(g=>Regions(g).Any(a=>regions.Any(b=>Overlap(a,b)))),"duplicate_source","Aceeași regiune sursă a fost deja verificată dintr-o încărcare duplicată.","This source region was already verified from a duplicate upload.",409);
  DocumentCapture.Link(report,line,document,svc,context,reason);
  var check=new Report{Id=report.Id,Purpose=report.Purpose,EmployeeId=report.EmployeeId,ManagerId=report.ManagerId,Payment=report.Payment,CardId=report.CardId,Lines=[line],Attachments=[source]};
  var issues=Rules.Validate(check,await db.Masters.ToListAsync());Guard.Require(!issues.Any(p=>p.Blocking),"ocr_validation",Serialization.Write(issues),Serialization.Write(issues));
  var before=new{group.State,group.CorrectedExpense,group.ExpenseId};group.State="Verified";group.ExpenseId=line.Id;group.VerifiedBy=Reports.Actor(context.User);group.VerifiedAt=DateTime.UtcNow;group.CorrectedExpense=line.Json;
  svc.Log(context.User,report.Id,document.ToString(),"DocumentVerified",before,new{group.Id,group.ExpenseId,group.VerifiedAt,group.CorrectedExpense,regions},reason);
 }
 public static void Map(RouteGroupBuilder api){
  api.MapGet("/reports/{id:guid}/documents/{document:guid}/review",async(Guid id,Guid document,Db db,Reports svc,HttpContext c)=>{
   var report=await svc.Get(id,c.User);Guard.Require(report.Attachments.Any(a=>a.Id==document&&!a.Archived),"missing","Document inexistent.","Document not found.",404);
   var review=await db.DocumentReviews.SingleOrDefaultAsync(r=>r.Id==document&&r.ReportId==id);return review==null?Results.Ok(new{available=false}):Results.Ok(new{available=true,review=await View(db,review)});
  });
  api.MapPost("/reports/{id:guid}/documents/{document:guid}/review/start",async(Guid id,Guid document,VersionRequest i,Db db,Reports svc,HttpContext c)=>await Api.Atomic(db,c,i,async()=>{
   var report=await svc.Version(id,i.Version,c.User);svc.Edit(report,c.User,i.Reason);var source=report.Attachments.Single(a=>a.Id==document&&!a.Archived);Guard.Require(source.Scan=="Clean"&&source.Ocr!=null,"ocr_pending","Așteaptă scanarea și extragerea.","Wait for scanning and extraction.");var result=Serialization.Read<OcrResult>(source.Ocr!);Guard.Require(result.Pages.Count>0,"ocr_pages","Paginile nu sunt disponibile. Reîncearcă extragerea.","Pages are unavailable. Retry extraction.");await Import(db,id,document,result);Reports.Touch(report);svc.Log(c.User,id,document.ToString(),"DocumentReviewStarted",reason:i.Reason);return Api.View(report);
  }));
  api.MapPost("/reports/{id:guid}/documents/{document:guid}/groups",async(Guid id,Guid document,GroupCommand i,Db db,Reports svc,HttpContext c)=>await Api.Atomic(db,c,i,async()=>{
   var report=await svc.Version(id,i.Version,c.User);svc.Edit(report,c.User,i.Reason);Guard.Reason(i.Reason);Guard.Require(report.Attachments.Any(a=>a.Id==document&&!a.Archived&&a.Scan=="Clean"),"source","Documentul sursă nu este disponibil.","Source document is unavailable.");
   var review=await db.DocumentReviews.SingleAsync(r=>r.Id==document&&r.ReportId==id);var all=await db.DocumentGroups.Where(g=>g.BundleId==document).ToListAsync();var ids=i.Groups??[];Guard.Require(ids.Distinct().Count()==ids.Length,"group","Selecție duplicată.","Duplicate selection.");var selected=all.Where(g=>ids.Contains(g.Id)).ToList();
   Guard.Require(selected.Count==ids.Length&&selected.All(g=>g.State=="ReviewRequired"),"group_state","Poți modifica doar grupuri neverificate. Bonurile verificate se corectează din decont.","Only unverified groups can be regrouped. Edit verified receipts from the report.");
   object before=selected.Select(g=>new{g.Id,g.State,g.Regions,g.Fields,g.Taxes}).ToList();List<DocumentGroup> added=[];
   DocumentGroup New(List<SourceRegion> regions,DocumentGroup? source=null){ValidateRegions(regions,review.PageCount);var g=new DocumentGroup{BundleId=document,SourceKey=Guid.NewGuid().ToString(),Regions=Serialization.Write(regions),Fields=source?.Fields??"{}",Taxes=source?.Taxes??"[]",Warnings=Serialization.Write(new[]{"Verifică toate valorile după delimitare. / Verify all fields after regrouping."}),Parents=Serialization.Write(ids)};added.Add(g);db.DocumentGroups.Add(g);return g;}
   if(i.Action=="add"){Guard.Require(ids.Length==0,"group","Nu selecta grupuri pentru un document nou.","Do not select groups for a new document.");New((i.Regions??[]).ToList());}
   else if(i.Action=="regions"){Guard.Require(selected.Count==1,"group","Selectează un grup.","Select one group.");ValidateRegions(i.Regions??[],review.PageCount);selected[0].Regions=Serialization.Write(i.Regions);}
   else if(i.Action=="merge"){Guard.Require(selected.Count>=2,"group","Selectează cel puțin două grupuri.","Select at least two groups.");New(selected.SelectMany(Regions).Distinct().OrderBy(r=>r.Page).ToList(),selected[0]);foreach(var g in selected)g.State="Merged";}
   else if(i.Action=="split"){Guard.Require(selected.Count==1,"group","Selectează un grup.","Select one group.");var split=(i.Pages??[]).ToHashSet();var regions=Regions(selected[0]);var a=regions.Where(r=>split.Contains(r.Page)).ToList();var b=regions.Where(r=>!split.Contains(r.Page)).ToList();Guard.Require(a.Count>0&&b.Count>0,"split","Alege o parte din paginile grupului.","Choose some of the group's pages.");New(a,selected[0]);New(b,selected[0]);selected[0].State="Split";}
   else if(i.Action=="supporting"||i.Action=="delete"){Guard.Require(selected.Count>0,"group","Selectează un grup.","Select a group.");var dispositions=Serialization.Read<List<PageDisposition>>(review.Dispositions);foreach(var g in selected){g.State=i.Action=="supporting"?"Supporting":"Deleted";if(i.Action=="supporting")foreach(var page in Regions(g).Select(r=>r.Page).Distinct())if(!dispositions.Any(p=>p.Page==page))dispositions.Add(new(page,i.Reason!));}review.Dispositions=Serialization.Write(dispositions);}
   else throw new DomainException("action","Acțiune invalidă.","Invalid action.");
   Reports.Touch(report);svc.Log(c.User,id,document.ToString(),"DocumentGroupsChanged",before,new{i.Action,groups=selected.Select(g=>new{g.Id,g.State,g.Regions}),created=added.Select(g=>new{g.Id,g.Regions,g.Parents})},i.Reason);return Api.View(report);
  }));
  api.MapPost("/reports/{id:guid}/documents/{document:guid}/pages",async(Guid id,Guid document,PageCommand i,Db db,Reports svc,HttpContext c)=>await Api.Atomic(db,c,i,async()=>{
   var report=await svc.Version(id,i.Version,c.User);svc.Edit(report,c.User,i.Reason);Guard.Reason(i.Reason);Guard.Require(report.Attachments.Any(a=>a.Id==document&&!a.Archived),"source","Document sursă indisponibil.","Source unavailable.");var review=await db.DocumentReviews.SingleAsync(r=>r.Id==document&&r.ReportId==id);Guard.Require(i.Page>=1&&i.Page<=review.PageCount,"page","Pagină invalidă.","Invalid page.");var before=review.Dispositions;var pages=Serialization.Read<List<PageDisposition>>(before);pages.RemoveAll(p=>p.Page==i.Page);if(i.Supporting)pages.Add(new(i.Page,i.Reason));review.Dispositions=Serialization.Write(pages);Reports.Touch(report);svc.Log(c.User,id,document.ToString(),"PageDispositionChanged",before,review.Dispositions,i.Reason);return Api.View(report);
  }));
  api.MapGet("/reports/{id:guid}/documents/{document:guid}/extractions",async(Guid id,Guid document,Db db,Reports svc,HttpContext c)=>{var report=await svc.Get(id,c.User);Guard.Require(report.Attachments.Any(a=>a.Id==document&&!a.Archived),"missing","Document inexistent.","Document not found.",404);return await db.ExtractionAttempts.Where(a=>a.BundleId==document).OrderBy(a=>a.Created).Select(a=>new{a.Id,a.Created,a.Fingerprint,a.Json}).ToListAsync();});
 }
}
