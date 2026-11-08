using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Decont;

public class ApprovalRoute {
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid ReportId { get; set; }
 public int Revision { get; set; }
 public Guid? AssignmentId { get; set; }
 public string ManagerId { get; set; } = "";
 public string AssigneeId { get; set; } = "";
 public Guid? DelegationId { get; set; }
 public string PolicyVersion { get; set; } = "single-company-direct-manager/owner-2026-11-08-50RON";
 public string SnapshotJson { get; set; } = "{}";
 public string SnapshotHash { get; set; } = "";
 public string ReportHash { get; set; } = "";
 public string Actor { get; set; } = "";
 public string Reason { get; set; } = "";
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ApprovalTask {
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid RouteId { get; set; }
 public Guid ReportId { get; set; }
 public int Revision { get; set; }
 public string AssigneeId { get; set; } = "";
 public string State { get; set; } = "Pending";
 public long Version { get; set; } = 1;
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime? CompletedAt { get; set; }
 public string? CompletedBy { get; set; }
 public string? Note { get; set; }
}
public record ResolvedManager(Assignment Assignment,User Manager,User Assignee,Guid? DelegationId);
public record ApprovalImpact(decimal ApprovedRon,decimal ProposedRon,decimal DifferenceRon,bool RequiresReapproval,decimal ThresholdRon=50);
public record CorrectionPreview(Guid? LineId,ExpenseData? Data,string Operation="replace");
public record TaskDecision(long Version,long ReportVersion,string? Reason);
public record ReassignTask(long Version,long ReportVersion,string ManagerId,string Reason);

public static class EmployeeIdentity {
 public static void Format(string value)=>Guard.Require(Regex.IsMatch(value??"",@"\A[A-Za-z]{6}[0-9]{2}\z"),"lantid_format","LantID trebuie să conțină șase litere și două cifre, de exemplu ABCDEF12.","LantID must contain six letters and two digits, for example ABCDEF12.");
 public static async Task Validate(Db db,string value,string? except=null){Format(value);var normalized=value.ToUpperInvariant();Guard.Require(!await db.Users.AnyAsync(u=>u.Id!=except&&u.Worker.ToUpper()==normalized),"lantid_duplicate","LantID există deja. Verifică angajatul.","LantID already exists. Check the employee.");}
}

public static class ApprovalWorkflow {
 public static void Configure(ModelBuilder b){
  b.Entity<ApprovalRoute>().HasOne<Report>().WithMany().HasForeignKey(x=>x.ReportId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ApprovalRoute>().HasOne<Assignment>().WithMany().HasForeignKey(x=>x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ApprovalRoute>().HasIndex(x=>new{x.ReportId,x.Revision});
  b.Entity<ApprovalTask>().HasOne<ApprovalRoute>().WithMany().HasForeignKey(x=>x.RouteId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ApprovalTask>().HasOne<Report>().WithMany().HasForeignKey(x=>x.ReportId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<ApprovalTask>().Property(x=>x.Version).IsConcurrencyToken();
  b.Entity<ApprovalTask>().HasIndex(x=>x.ReportId).IsUnique().HasFilter("\"State\" = 'Pending'");
  b.Entity<ApprovalTask>().HasIndex(x=>new{x.AssigneeId,x.State,x.CreatedAt});
 }
 public static void Protect(ChangeTracker tracker){
  foreach(var e in tracker.Entries<ApprovalTask>())
   if(e.State==EntityState.Deleted||e.State==EntityState.Modified&&e.Properties.Any(p=>p.IsModified&&p.Metadata.Name is not ("State" or "Version" or "CompletedAt" or "CompletedBy" or "Note")))throw new InvalidOperationException("Immutable approval task authority");
  foreach(var e in tracker.Entries<ApprovalTask>().Where(x=>x.State==EntityState.Modified))if(e.OriginalValues.GetValue<string>("State")!="Pending"||e.Entity.State is not ("Approved" or "Returned" or "Rejected" or "Reassigned")||e.Entity.Version!=e.OriginalValues.GetValue<long>("Version")+1)throw new InvalidOperationException("Invalid approval task transition");
 }
 static DateOnly Day()=>DateOnly.FromDateTime(DateTime.UtcNow);
 static async Task<bool> HasManagerRole(Db db,string id)=>await db.UserRoles.Join(db.Roles,x=>x.RoleId,y=>y.Id,(x,y)=>new{x.UserId,y.Name}).AnyAsync(x=>x.UserId==id&&x.Name=="Manager");
 public static async Task ValidateAssignment(Db db,string employee,string manager,DateOnly from,DateOnly? to,Guid? except=null){
  Guard.Require(employee!=manager&&from!=default&&from<=(to??DateOnly.MaxValue),"manager_cycle","Alocarea nu poate indica angajatul însuși și trebuie să aibă o perioadă validă.","Assignment must have a valid period and cannot point to the employee.");
  var rows=await db.Assignments.Where(x=>!x.Superseded&&x.Active&&x.Id!=except).ToListAsync();
  // Evaluate each boundary: a cycle can begin at a future assignment start.
  var boundaries=rows.Select(x=>x.From).Concat(rows.Where(x=>x.To!=null&&x.To<DateOnly.MaxValue).Select(x=>x.To!.Value.AddDays(1))).Append(from).Where(x=>x>=from&&x<=(to??DateOnly.MaxValue)).Distinct();
  foreach(var day in boundaries){var seen=new HashSet<string>{employee};var cursor=manager;while(!string.IsNullOrEmpty(cursor)){Guard.Require(seen.Add(cursor),"manager_cycle","Alocarea creează un ciclu în organigramă. Verifică managerii și datele.","Assignment creates an organisation cycle. Check managers and dates.");var candidates=rows.Where(x=>x.UserId==cursor&&x.From<=day&&(x.To==null||x.To>=day)).ToList();Guard.Require(candidates.Count<=1,"manager_overlap","Există alocări suprapuse. Administratorul trebuie să le corecteze.","Assignments overlap. An administrator must correct them.");cursor=candidates.SingleOrDefault()?.ManagerId??"";}}
 }
 public static async Task SetProfileManager(Db db,User employee,string? manager,ClaimsPrincipal actor,Reports svc,string reason,string? department=null){
  var day=Day();var rows=await db.Assignments.Where(x=>!x.Superseded&&x.UserId==employee.Id&&x.From<=day&&(x.To==null||x.To>=day)).ToListAsync();
  if(string.IsNullOrEmpty(manager)&&rows.Any(x=>!x.Active))return;if(rows.Count==1&&rows[0].ManagerId==manager&&(department==null||department==rows[0].OrganizationCode))return;if(!string.IsNullOrEmpty(department))Guard.Require(await db.Masters.AnyAsync(m=>m.Kind=="organizationUnits"&&m.Code==department&&m.Active&&m.From<=day&&(m.To==null||m.To>=day)),"department","Selectează un departament activ din nomenclator.","Select an active department from master data.");Guard.Require(!await db.Assignments.AnyAsync(x=>!x.Superseded&&x.UserId==employee.Id&&x.From>day),"assignment_future","Există o alocare viitoare. Editează perioadele din organigramă înainte de schimbarea managerului.","A future assignment exists. Update organisation periods before changing the manager.",409);
  if(!string.IsNullOrEmpty(manager))await ValidateAssignment(db,employee.Id,manager,day,null);
  foreach(var row in rows){if(row.From<day)row.To=day.AddDays(-1);else row.Superseded=true;svc.Log(actor,null,row.Id.ToString(),"AssignmentSuperseded",reason:reason);}
  if(!string.IsNullOrEmpty(manager)){var assignment=new Assignment{UserId=employee.Id,ManagerId=manager,From=day,Active=rows.FirstOrDefault()?.Active??true,OrganizationCode=department??rows.FirstOrDefault()?.OrganizationCode??""};db.Assignments.Add(assignment);svc.Log(actor,null,assignment.Id.ToString(),"AssignmentCreated",after:assignment,reason:reason);}
 }
 public static async Task<ResolvedManager> Resolve(Db db,string employeeId,DateOnly day){
  var employee=await db.Users.FindAsync(employeeId);Guard.Require(employee!=null&&await PlatformFeatures.Active(db,employee,day),"manager_employee","Angajatul nu este activ. Contactează administratorul.","Employee is inactive. Contact the administrator.");
  var assignments=await db.Assignments.Where(x=>!x.Superseded&&x.UserId==employeeId&&x.Active&&x.From<=day&&(x.To==null||x.To>=day)).ToListAsync();
  Guard.Require(assignments.Count==1,"manager_assignment","Lipsește o alocare unică de manager. Administratorul trebuie să actualizeze organigrama.","A unique manager assignment is missing. Ask the administrator to update the organisation chart.");
  var assignment=assignments[0];await ValidateAssignment(db,employeeId,assignment.ManagerId,day,day,assignment.Id);
  var manager=await db.Users.FindAsync(assignment.ManagerId);Guard.Require(manager!=null&&manager.Id!=employeeId&&await PlatformFeatures.Active(db,manager,day)&&await HasManagerRole(db,manager.Id),"manager","Managerul nu este activ sau nu are acces la aprobare. Contactează administratorul.","Manager is inactive or cannot approve. Contact the administrator.");
  var delegations=await db.Delegations.Where(x=>x.ManagerId==manager!.Id&&x.From<=day&&x.To>=day).ToListAsync();Guard.Require(delegations.Count<=1,"delegation_overlap","Delegări suprapuse. Contactează administratorul.","Delegations overlap. Contact the administrator.");
  var delegation=delegations.SingleOrDefault();var assignee=delegation==null?manager:await db.Users.FindAsync(delegation.DelegateId);
  Guard.Require(assignee!=null&&assignee.Id!=employeeId&&await PlatformFeatures.Active(db,assignee,day)&&await HasManagerRole(db,assignee.Id),"delegation","Delegatul nu poate aproba acest decont. Contactează administratorul.","The delegate cannot approve this report. Contact the administrator.");
  return new(assignment,manager!,assignee!,delegation?.Id);
 }
 public static async Task CreateTask(Db db,Reports svc,Report report,ClaimsPrincipal actor,ResolvedManager resolved,string reason="Trimitere / Submission"){
  Guard.Require(!await db.ApprovalTasks.AnyAsync(x=>x.ReportId==report.Id&&x.State=="Pending")&&!db.ApprovalTasks.Local.Any(x=>x.ReportId==report.Id&&x.State=="Pending"),"approval_pending","Există deja o aprobare în curs.","An approval is already pending.",409);
  var snapshot=db.Snapshots.Local.SingleOrDefault(x=>x.ReportId==report.Id&&x.Revision==report.Revision)??await db.Snapshots.SingleAsync(x=>x.ReportId==report.Id&&x.Revision==report.Revision);
  var delegation=resolved.DelegationId==null?null:await db.Delegations.FindAsync(resolved.DelegationId.Value);var json=Serialization.Write(new{delegation,assignmentId=resolved.Assignment.Id,resolved.Assignment.From,resolved.Assignment.To,department=resolved.Assignment.OrganizationCode,managerId=resolved.Manager.Id,managerName=resolved.Manager.Name,managerRole="Manager",assigneeId=resolved.Assignee.Id,assigneeName=resolved.Assignee.Name,delegationId=resolved.DelegationId});
  var route=new ApprovalRoute{ReportId=report.Id,Revision=report.Revision,AssignmentId=resolved.Assignment.Id,ManagerId=resolved.Manager.Id,AssigneeId=resolved.Assignee.Id,DelegationId=resolved.DelegationId,SnapshotJson=json,SnapshotHash=Serialization.Hash(json),ReportHash=snapshot.Hash,Actor=Reports.Actor(actor),Reason=reason};
  db.ApprovalRoutes.Add(route);db.ApprovalTasks.Add(new(){RouteId=route.Id,ReportId=report.Id,Revision=report.Revision,AssigneeId=route.AssigneeId});
  svc.Log(actor,report.Id,route.Id.ToString(),"ApprovalTaskCreated",after:new{route.AssignmentId,route.ManagerId,route.AssigneeId,route.Revision,route.ReportHash,route.PolicyVersion},reason:reason);
  if(resolved.Assignee.Id!=report.ManagerId)await svc.Notify(report,resolved.Assignee.Id,"Submitted");
 }
 public static async Task CreateReapproval(Db db,Reports svc,Report report,ClaimsPrincipal actor){
  // Reapproval preserves the route selected by Admin, not a newly changed org chart.
  var previous=await db.ApprovalRoutes.Where(x=>x.ReportId==report.Id).OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync();
  var manager=await db.Users.FindAsync(report.ManagerId);Guard.Require(manager!=null&&manager.Active&&await HasManagerRole(db,manager.Id),"manager","Managerul trebuie actualizat de Admin înainte de reaprobare.","Admin must update the manager before reapproval.");
  var assignment=previous?.AssignmentId==null?await db.Assignments.Where(x=>!x.Superseded&&x.UserId==report.EmployeeId&&x.ManagerId==report.ManagerId&&x.Active).OrderByDescending(x=>x.From).FirstOrDefaultAsync():await db.Assignments.FindAsync(previous.AssignmentId.Value);
  Guard.Require(assignment!=null,"manager_assignment","Admin trebuie să confirme alocarea managerului.","Admin must confirm the manager assignment.");
  await CreateTask(db,svc,report,actor,new(assignment!,manager!,manager!,null),"Corecție cumulată peste 50 RON / Cumulative correction above 50 RON");
 }
 public static async Task<bool> CanDecide(Db db,Report report,ClaimsPrincipal actor){
  if(!actor.IsInRole("Manager")||report.EmployeeId==Reports.Actor(actor)||report.State is not (States.Manager or States.Reapproval))return false;
  var task=await db.ApprovalTasks.SingleOrDefaultAsync(x=>x.ReportId==report.Id&&x.Revision==report.Revision&&x.State=="Pending");if(task==null||task.AssigneeId!=Reports.Actor(actor))return false;
  var user=await db.Users.FindAsync(task.AssigneeId);if(user==null||!await PlatformFeatures.Active(db,user,Day())||!await HasManagerRole(db,user.Id))return false;
  var route=await db.ApprovalRoutes.FindAsync(task.RouteId);if(route==null)return false;var originalManager=await db.Users.FindAsync(route.ManagerId);if(originalManager==null||!await PlatformFeatures.Active(db,originalManager,Day())||!await HasManagerRole(db,originalManager.Id))return false;
  if(route.DelegationId!=null&&!await db.Delegations.AnyAsync(d=>d.Id==route.DelegationId&&d.DelegateId==task.AssigneeId&&d.From<=Day()&&d.To>=Day()))return false;
  return true;
 }
 public static async Task CompleteTask(Db db,Report report,ClaimsPrincipal actor,string action,string? reason){
  var task=await db.ApprovalTasks.SingleAsync(x=>x.ReportId==report.Id&&x.Revision==report.Revision&&x.State=="Pending");task.State=action=="approve"?"Approved":action=="return"?"Returned":"Rejected";task.CompletedAt=DateTime.UtcNow;task.CompletedBy=Reports.Actor(actor);task.Note=reason;task.Version++;
 }
 public static async Task<ApprovalImpact> Impact(Db db,Report report,CorrectionPreview? preview=null){
  var snapshot=await db.Snapshots.SingleOrDefaultAsync(x=>x.ReportId==report.Id&&x.Revision==report.Revision);
  if(snapshot==null){var total=report.Lines.Where(x=>!x.Archived).Sum(x=>x.EstimatedRon);return report.ApprovedPersonal==0?new(total,total,0,false):new(report.ApprovedPersonal,total,Rules.Money(total-report.ApprovedPersonal),Math.Abs(total-report.ApprovedPersonal)>50);}
  using var json=JsonDocument.Parse(snapshot.Json);var old=json.RootElement.GetProperty("lines").EnumerateArray().ToDictionary(x=>x.GetProperty("id").GetGuid(),x=>x.Clone());
  decimal baseline=old.Values.Sum(x=>x.GetProperty("estimatedRon").GetDecimal()),current=0;
  foreach(var line in report.Lines.Where(x=>!x.Archived)){
   if(preview?.Operation=="archive"&&preview.LineId==line.Id)continue;
   var data=preview?.LineId==line.Id&&preview.Data!=null?preview.Data:line.Data();
   if(old.TryGetValue(line.Id,out var prior)){var before=Serialization.Read<ExpenseData>(prior.GetProperty("json").GetString()!);if(before.Currency==data.Currency&&before.Amount!=0){current+=data.Amount*prior.GetProperty("estimatedRon").GetDecimal()/before.Amount;continue;}}
   current+=data.Currency=="RON"?data.Amount:data.Amount*(await FinancialCalculations.Estimate(db,data.Currency)).Rate;
  }
  if(preview?.Operation=="add"&&preview.Data!=null)current+=preview.Data.Currency=="RON"?preview.Data.Amount:preview.Data.Amount*(await FinancialCalculations.Estimate(db,preview.Data.Currency)).Rate;
  if(preview?.Operation=="duplicate"&&preview.LineId!=null){var line=report.Lines.Single(x=>x.Id==preview.LineId&&!x.Archived);var data=line.Data();current+=old.TryGetValue(line.Id,out var p)?p.GetProperty("estimatedRon").GetDecimal():line.EstimatedRon;}
  current=Rules.Money(current);baseline=Rules.Money(baseline);var difference=Rules.Money(current-baseline);return new(baseline,current,difference,Math.Abs(difference)>50);
 }
 public static void Map(RouteGroupBuilder api){
  api.MapGet("/org/me/manager",async(Db db,HttpContext c,string? as_of)=>{var day=as_of==null?Day():DateOnly.TryParse(as_of,out var d)?d:throw new DomainException("date","Data nu este validă.","Invalid date.");var result=await Resolve(db,Reports.Actor(c.User),day);return new{managerId=result.Manager.Id,managerName=result.Manager.Name,assigneeId=result.Assignee.Id,assignmentId=result.Assignment.Id,result.Assignment.OrganizationCode};});
  api.MapPost("/finance/reports/{id:guid}/change-preview",async(Guid id,CorrectionPreview i,Db db,Reports svc,HttpContext c)=>{var r=await svc.Get(id,c.User);Guard.Require(States.FinanceEdit(r.State),"state","Decontul nu este în verificare Finance.","Report is not in Finance review.");Guard.Require(i.Operation is "replace" or "add" or "archive" or "duplicate","operation","Operațiune invalidă.","Invalid operation.");if(i.LineId!=null)Guard.Require(r.Lines.Any(x=>x.Id==i.LineId&&!x.Archived),"line","Bon inexistent.","Receipt not found.",404);return await Impact(db,r,i);}).RequireAuthorization(p=>p.RequireRole("Finance"));
  api.MapGet("/approvals/inbox",async(Db db,Reports svc,HttpContext c)=>{var id=Reports.Actor(c.User);var visible=svc.Scope(c.User).Select(r=>r.Id);return await db.ApprovalTasks.Where(t=>visible.Contains(t.ReportId)&&t.AssigneeId==id&&t.State=="Pending").OrderBy(t=>t.CreatedAt).Take(200).ToListAsync();}).RequireAuthorization(p=>p.RequireRole("Manager"));
  api.MapGet("/approvals/tasks/{id:guid}",async(Guid id,Db db,Reports svc,HttpContext c)=>{var task=await db.ApprovalTasks.FindAsync(id)??throw new DomainException("missing","Aprobarea nu există.","Approval not found.",404);var report=await svc.Get(task.ReportId,c.User);Guard.Require(task.AssigneeId==Reports.Actor(c.User)||c.User.IsInRole("Admin")||c.User.IsInRole("Finance")||c.User.IsInRole("Auditor"),"forbidden","Nu ai acces la această aprobare.","Cannot access this approval.",403);return new{task,route=await db.ApprovalRoutes.FindAsync(task.RouteId),report=Api.View(report)};});
  api.MapPost("/approvals/tasks/{id:guid}/{action}",async(Guid id,string action,TaskDecision i,Db db,Reports svc,HttpContext c)=>await Api.Atomic(db,c,i,async()=>{Guard.Require(action is "approve" or "return" or "reject","action","Acțiune invalidă.","Invalid action.");var task=await db.ApprovalTasks.FindAsync(id)??throw new DomainException("missing","Aprobarea nu există.","Approval not found.",404);await svc.Get(task.ReportId,c.User);Guard.Require(task.AssigneeId==Reports.Actor(c.User),"forbidden","Aprobarea aparține altui responsabil.","The approval belongs to another assignee.",403);Guard.Require(task.Version==i.Version&&task.State=="Pending","conflict","Aprobarea a fost deja modificată. Reîncarcă.","Approval changed. Reload.",409);var r=await svc.Version(task.ReportId,i.ReportVersion,c.User);await svc.Action(r,c.User,action,i.Reason);return Api.View(r);})).RequireAuthorization(p=>p.RequireRole("Manager"));
  api.MapGet("/admin/approval-issues",async(Db db)=>{var day=Day();var people=await db.Users.Where(x=>x.Active).ToListAsync();var issues=new List<object>();foreach(var employee in people){if(!await db.UserRoles.Join(db.Roles,x=>x.RoleId,y=>y.Id,(x,y)=>new{x.UserId,y.Name}).AnyAsync(x=>x.UserId==employee.Id&&x.Name=="Employee"))continue;try{EmployeeIdentity.Format(employee.Worker);await Resolve(db,employee.Id,day);}catch(DomainException e){issues.Add(new{employee.Id,employee.Name,employee.Worker,code=e.Code,ro=e.Message,en=e.En});}}var pending=await db.Reports.Where(r=>r.State==States.Manager||r.State==States.Reapproval).ToListAsync();var unresolved=new List<object>();foreach(var report in pending){var task=await db.ApprovalTasks.SingleOrDefaultAsync(t=>t.ReportId==report.Id&&t.Revision==report.Revision&&t.State=="Pending");var principal=task==null?null:new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,task.AssigneeId),new(ClaimTypes.Role,"Manager")],"availability-check"));if(task==null||!await CanDecide(db,report,principal!))unresolved.Add(new{report.Id,report.Number,report.EmployeeId,report.ManagerId,report.Version,report.Revision,code=task==null?"legacy_confirmation":"assignee_unavailable"});}return new{people=issues,reports=unresolved};}).RequireAuthorization(p=>p.RequireRole("Admin"));
  api.MapPost("/admin/reports/{id:guid}/reassign",async(Guid id,ReassignTask i,Db db,Reports svc,HttpContext c)=>await Api.Atomic(db,c,i,async()=>{
   Guard.Reason(i.Reason);var report=await svc.Version(id,i.ReportVersion,c.User);Guard.Require(report.State is States.Manager or States.Reapproval,"state","Reasignarea se face numai pentru o aprobare în curs.","Only pending approvals can be reassigned.");
   var current=await db.ApprovalTasks.SingleOrDefaultAsync(t=>t.ReportId==id&&t.State=="Pending");Guard.Require(current==null?i.Version==0:current.Version==i.Version,"conflict","Aprobarea s-a modificat. Reîncarcă.","Approval changed. Reload.",409);
   var manager=await db.Users.FindAsync(i.ManagerId);Guard.Require(manager!=null&&manager.Id!=report.EmployeeId&&await PlatformFeatures.Active(db,manager,Day())&&await HasManagerRole(db,manager.Id),"manager","Selectează un manager activ, diferit de angajat.","Select an active manager other than the employee.");
   var assignments=await db.Assignments.Where(x=>!x.Superseded&&x.UserId==report.EmployeeId&&x.Active&&x.ManagerId==i.ManagerId&&x.From<=Day()&&(x.To==null||x.To>=Day())).ToListAsync();Guard.Require(assignments.Count==1,"manager_assignment","Actualizează mai întâi alocarea organizațională a angajatului.","Update the employee's organisation assignment first.");await ValidateAssignment(db,report.EmployeeId,i.ManagerId,Day(),Day(),assignments[0].Id);
   var before=report.ManagerId;if(current!=null){current.State="Reassigned";current.CompletedAt=DateTime.UtcNow;current.CompletedBy=Reports.Actor(c.User);current.Note=i.Reason;current.Version++;await db.SaveChangesAsync();}
   report.ManagerId=i.ManagerId;if(!await db.Snapshots.AnyAsync(x=>x.ReportId==report.Id&&x.Revision==report.Revision))svc.Snapshot(report,c.User);await CreateTask(db,svc,report,c.User,new(assignments[0],manager!,manager!,null),i.Reason);Reports.Touch(report);svc.Log(c.User,id,"Report","ApprovalReassigned",before,new{report.ManagerId,report.Revision},i.Reason);await svc.Notify(report,manager!.Id,"Submitted");return Api.View(report);
  })).RequireAuthorization(p=>p.RequireRole("Admin"));
 }
}
