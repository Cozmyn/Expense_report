using Decont;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Xunit;
namespace Decont.Tests;
public class Pdf05HttpTests {
 static async Task<HttpClient> Login(Factory f,string name){var c=f.CreateClient(new(){HandleCookies=true,AllowAutoRedirect=false});var csrf=await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/auth/login",new{username=name,password="Valid-test-Password1!"})).StatusCode);csrf=await c.GetFromJsonAsync<JsonElement>("/api/auth/csrf");c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());return c;}
 static void Key(HttpClient c){c.DefaultRequestHeaders.Remove("Idempotency-Key");c.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());}
 [Fact]public async Task AdminReassignmentKeepsRevisionRevokesFormerManagerAndRejectsStaleDecision(){
  await using var f=new Factory();await f.Seed();Guid reportId,oldTask;string targetId;int revision;
  using(var scope=f.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<Db>();var users=scope.ServiceProvider.GetRequiredService<UserManager<User>>();var target=(await users.FindByNameAsync("admin"))!;await users.AddToRoleAsync(target,"Manager");targetId=target.Id;var employee=(await users.FindByNameAsync("alice"))!;var resolved=await ApprovalWorkflow.Resolve(db,employee.Id,DateOnly.FromDateTime(DateTime.UtcNow));var r=new Report{EmployeeId=employee.Id,ManagerId=resolved.Manager.Id,State=States.Manager};db.Reports.Add(r);var actor=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,employee.Id)],"test"));var svc=new Reports(db);svc.Snapshot(r,actor);await db.SaveChangesAsync();await ApprovalWorkflow.CreateTask(db,svc,r,actor,resolved);await db.SaveChangesAsync();reportId=r.Id;revision=r.Revision;oldTask=(await db.ApprovalTasks.SingleAsync()).Id;resolved.Assignment.Active=false;db.Assignments.Add(new(){UserId=employee.Id,ManagerId=targetId,From=new(2020,1,1)});await db.SaveChangesAsync();
  }
  var former=await Login(f,"manager");Assert.Equal(HttpStatusCode.OK,(await former.GetAsync($"/api/reports/{reportId}")).StatusCode);var admin=await Login(f,"admin");Key(admin);
  var body=new ReassignTask(1,1,targetId,"Approved organisational reassignment");var result=await admin.PostAsJsonAsync($"/api/admin/reports/{reportId}/reassign",body);Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());Assert.Equal(revision,(await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
  Assert.Equal(HttpStatusCode.NotFound,(await former.GetAsync($"/api/reports/{reportId}")).StatusCode);Key(former);var stale=await former.PostAsJsonAsync($"/api/approvals/tasks/{oldTask}/approve",new TaskDecision(1,1,null));Assert.Equal(HttpStatusCode.NotFound,stale.StatusCode);
  using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<Db>();Assert.Equal(2,await db.ApprovalRoutes.CountAsync());Assert.Single(await db.ApprovalTasks.Where(t=>t.State=="Pending").ToListAsync());Assert.Single(await db.ApprovalTasks.Where(t=>t.State=="Reassigned").ToListAsync());Assert.Single(await db.Audits.Where(t=>t.Action=="ApprovalReassigned").ToListAsync());}
  // A new HTTP retry key still cannot apply an obsolete task/report version.
  Key(admin);Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/admin/reports/{reportId}/reassign",body)).StatusCode);
 }
 [Fact]public async Task EmployeeCannotAssignManagerOrInspectAnotherTask(){await using var f=new Factory();await f.Seed();var employee=await Login(f,"alice");Key(employee);Assert.Equal(HttpStatusCode.Forbidden,(await employee.PostAsJsonAsync($"/api/admin/reports/{Guid.NewGuid()}/reassign",new ReassignTask(0,1,"manager","Not permitted"))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await employee.GetAsync("/api/admin/approval-issues")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await employee.GetAsync("/api/approvals/inbox")).StatusCode);}
}
