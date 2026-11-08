using Decont;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace Decont.Tests;
public class Pdf05TransactionTests {
 sealed class FailAfterApprovalSave:SaveChangesInterceptor {
  public bool Enabled {get;set;}
  public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken cancellationToken=default){
   if(Enabled&&data.Context is Db db&&db.Database.CurrentTransaction!=null&&db.ChangeTracker.Entries<ApprovalTask>().Any(t=>t.Entity.State=="Pending")){Enabled=false;throw new InvalidOperationException("SYNTHETIC failure after SQL save and before transaction commit");}
   return ValueTask.FromResult(result);
  }
 }
 [Fact]public async Task SubmitFailureRollsBackRouteTaskSnapshotAuditAndOutboxAndSameKeyRetryConverges(){
  var fault=new FailAfterApprovalSave();await using var f=new Factory{DatabaseInterceptor=fault};await f.Seed();Guid reportId;string employeeId;
  using(var scope=f.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<Db>();var employee=await db.Users.SingleAsync(u=>u.UserName=="alice");employeeId=employee.Id;
   var data=new ExpenseData("",null,"SYNTHETIC-1","2026-01-01","DE","Receipt","GENERAL","Synthetic rollback fixture","Personal",null,"CC",null,100,"RON",[],false);
   var report=new Report{EmployeeId=employee.Id,Purpose="Synthetic transactional submission",Payment="Personal",Lines=[new(){Position=1,EstimatedRon=100,Json=Serialization.Write(data),Evidence=[new(){Scan="Clean",Hash=new string('A',64),Name="synthetic.png",Mime="image/png"}]}]};
   db.Reports.Add(report);db.Masters.AddRange(new Master{Kind="expenseTypes",Code="GENERAL",From=new(2020,1,1)},new Master{Kind="costCenters",Code="CC",Json="{\"erpCode\":\"CC\"}",From=new(2020,1,1)},new Master{Kind="notificationRules",Code="Submitted",Json="{\"inApp\":true,\"email\":true}",From=new(2020,1,1)});await db.SaveChangesAsync();reportId=report.Id;
  }
  var client=f.CreateClient(new(){HandleCookies=true,AllowAutoRedirect=false});var csrf=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/login",new{username="alice",password="Valid-test-Password1!"})).StatusCode);csrf=await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());client.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());fault.Enabled=true;
  var failed=await client.PostAsJsonAsync($"/api/reports/{reportId}/submit",new VersionRequest(1,null));Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
  using(var scope=f.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<Db>();var report=await db.Reports.SingleAsync(r=>r.Id==reportId);Assert.Equal(States.Draft,report.State);Assert.Equal(0,report.Revision);Assert.Equal(1,report.Version);Assert.Empty(await db.ApprovalTasks.ToListAsync());Assert.Empty(await db.ApprovalRoutes.ToListAsync());Assert.Empty(await db.Snapshots.ToListAsync());Assert.Empty(await db.Notices.ToListAsync());Assert.Empty(await db.Jobs.ToListAsync());Assert.Empty(await db.Audits.Where(a=>a.ReportId==reportId).ToListAsync());Assert.Empty(await db.Idempotencies.Where(i=>i.Actor==employeeId).ToListAsync());
  }
  var retry=await client.PostAsJsonAsync($"/api/reports/{reportId}/submit",new VersionRequest(1,null));Assert.True(retry.IsSuccessStatusCode,await retry.Content.ReadAsStringAsync());
  var repeated=await client.PostAsJsonAsync($"/api/reports/{reportId}/submit",new VersionRequest(1,null));Assert.Equal(await retry.Content.ReadAsStringAsync(),await repeated.Content.ReadAsStringAsync());
  using(var scope=f.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<Db>();Assert.Single(await db.ApprovalTasks.ToListAsync());Assert.Single(await db.ApprovalRoutes.ToListAsync());Assert.Single(await db.Snapshots.ToListAsync());Assert.Single(await db.Notices.ToListAsync());Assert.Single(await db.Jobs.Where(j=>j.Kind=="Email").ToListAsync());Assert.Single(await db.Audits.Where(a=>a.Action=="Submitted").ToListAsync());Assert.Single(await db.Idempotencies.Where(i=>i.Actor==employeeId).ToListAsync());
  }
 }
}
