using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record RoutingInput(long Version, string EmployeeId, string ManagerId, string Reason);

public static class FinanceRouting
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
                "/directory",
                async (Db db) =>
                {
                    var users = await db
                        .Users.Where(u => u.Active)
                        .Select(u => new
                        {
                            u.Id,
                            u.Name,
                            u.Worker,
                        })
                        .ToListAsync();
                    var memberships = await db
                        .UserRoles.Join(db.Roles, x => x.RoleId, y => y.Id, (x, y) => new { x.UserId, y.Name })
                        .ToListAsync();
                    return users.Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Worker,
                        roles = memberships.Where(x => x.UserId == u.Id).Select(x => x.Name),
                    });
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance", "Admin"));
        api.MapPost(
                "/finance/reports/{id:guid}/routing",
                async (Guid id, RoutingInput i, Db db, Reports svc, UserManager<User> users, HttpContext c) =>
                    await Api.Atomic(
                        db,
                        c,
                        i,
                        async () =>
                        {
                            var r = await svc.Version(id, i.Version, c.User);
                            Guard.Require(
                                States.FinanceEdit(r.State),
                                "state",
                                "Corecția se face în verificarea Finance.",
                                "Routing can be corrected during Finance review."
                            );
                            svc.Edit(r, c.User, i.Reason);
                            Guard.Reason(i.Reason);
                            var employee = await users.FindByIdAsync(i.EmployeeId);
                            var manager = await users.FindByIdAsync(i.ManagerId);
                            Guard.Require(
                                employee != null
                                    && await PlatformFeatures.Active(
                                        db,
                                        employee,
                                        DateOnly.FromDateTime(DateTime.UtcNow)
                                    )
                                    && await users.IsInRoleAsync(employee, "Employee"),
                                "employee",
                                "Selectează un angajat activ.",
                                "Select an active employee."
                            );
                            Guard.Require(
                                manager != null
                                    && manager.Active
                                    && manager.Id != employee!.Id
                                    && await users.IsInRoleAsync(manager, "Manager"),
                                "manager",
                                "Selectează un manager activ, diferit de angajat.",
                                "Select an active manager different from the employee."
                            );
                            Guard.Require(
                                r.EmployeeId != i.EmployeeId || r.ManagerId != i.ManagerId,
                                "unchanged",
                                "Alocarea nu s-a modificat.",
                                "Routing did not change."
                            );
                            var before = new { r.EmployeeId, r.ManagerId };
                            r.EmployeeId = i.EmployeeId;
                            r.ManagerId = i.ManagerId;
                            svc.Snapshot(r, c.User);
                            r.State = States.Reapproval;
                            Reports.Touch(r);
                            svc.Log(
                                c.User,
                                id,
                                "Report",
                                "ReportRoutingCorrected",
                                before,
                                new { r.EmployeeId, r.ManagerId },
                                i.Reason
                            );
                            await svc.Notify(r, r.ManagerId, "ReapprovalRequired");
                            return Api.View(r);
                        }
                    )
            )
            .RequireAuthorization(p => p.RequireRole("Finance"));
    }
}
