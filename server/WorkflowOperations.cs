using Microsoft.EntityFrameworkCore;
namespace Decont;

public static class WorkflowOperations {
 public static bool IsConflict(Exception error){for(Exception? current=error;current!=null;current=current.InnerException){if(current is Npgsql.PostgresException pg&&pg.SqlState is "40001" or "40P01" or "23505")return true;if(current is Microsoft.Data.Sqlite.SqliteException sqlite&&sqlite.SqliteErrorCode is 5 or 6 or 19)return true;}return false;}
 public static async Task RecordDenied(IServiceProvider services,HttpContext context,string code){if(context.User.Identity?.IsAuthenticated!=true)return;using var scope=services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<Db>();db.Audits.Add(new(){Actor=Reports.Actor(context.User),Entity="Authorization",Action="AccessDenied",RequestId=context.TraceIdentifier,After=Serialization.Write(new{code,method=context.Request.Method})});await db.SaveChangesAsync();}
 public static void Map(RouteGroupBuilder api){
  api.MapGet("/admin/workflow-metrics",async(Db db)=>{
   var now=DateTime.UtcNow;var reviewStates=new[]{States.Finance,States.Ready,"PENDING_VENDOR","PENDING_CARD_MATCH"};var reviewReports=db.Reports.Where(r=>reviewStates.Contains(r.State)).Select(r=>r.Id);var fxLines=await db.Lines.Where(l=>!l.Archived&&l.FxDecisionId==null&&reviewReports.Contains(l.ReportId)).Select(l=>l.Json).ToListAsync();
   var oldest=await db.ApprovalTasks.Where(t=>t.State=="Pending").OrderBy(t=>t.CreatedAt).Select(t=>(DateTime?)t.CreatedAt).FirstOrDefaultAsync();
   var queue=await db.Jobs.Where(j=>j.State=="Pending").OrderBy(j=>j.Due).Select(j=>(DateTime?)j.Due).FirstOrDefaultAsync();
   return new{asOf=now,pendingApprovals=await db.ApprovalTasks.CountAsync(t=>t.State=="Pending"),oldestApprovalHours=oldest==null?0:Math.Max(0,(now-oldest.Value).TotalHours),pendingVendors=await db.Vendors.CountAsync(v=>v.State=="Pending"),pendingFxProposals=fxLines.Count(json=>Serialization.Read<ExpenseData>(json).Currency!="RON"),failedJobs=await db.Jobs.CountAsync(j=>j.State=="Failed"),oldestJobOverdueMinutes=queue==null?0:Math.Max(0,(now-queue.Value).TotalMinutes),notificationOutbox=await db.Jobs.CountAsync(j=>j.Kind=="Email"&&(j.State=="Pending"||j.State=="Processing")),postingRejections=await db.PostingReviews.CountAsync(p=>p.Action=="REJECT"),reportsByState=await db.Reports.GroupBy(r=>r.State).Select(g=>new{state=g.Key,count=g.Count()}).ToListAsync(),schema=await db.SchemaReleases.MaxAsync(r=>(int?)r.Id)??0};
  }).RequireAuthorization(p=>p.RequireRole("Admin"));
 }
}
