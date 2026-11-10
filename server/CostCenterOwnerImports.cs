using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record CostCenterOwnerImportRow(string CostCenterCode, string ManagerUsername);
public record CostCenterOwnerImportBatch(List<CostCenterOwnerImportRow> Rows, string Reason);

/// <summary>
/// CSV/XLSX bulk ownership changes are validated before any mutation.
/// The original approval evidence and audited re-routing are retained.
/// </summary>
public static class CostCenterOwnerImports
{
    public static void Map(RouteGroupBuilder api)
    {
        var root = api.MapGroup("/admin/cost-center-owners")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        root.MapGet("/template", () => ImportExamples.Download("cost-center-owners"));
        root.MapPost("/import/preview", async (HttpContext c, Db db) =>
        {
            var (rows, hash) = await SpreadsheetImports.Upload(c);
            Guard.Require(rows.Count > 1, "rows", "Fișierul este gol.", "Empty file.");
            Guard.Require(rows[0].Contains("costCenterCode") && rows[0].Contains("managerUsername"),
                "headers",
                "Lipsesc coloanele costCenterCode și managerUsername.",
                "Missing costCenterCode and managerUsername columns.");
            var entries = new List<CostCenterOwnerImportRow>();
            var errors = new List<object>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var n = 1; n < rows.Count; n++)
            {
                string Get(string k)
                {
                    var index = Array.IndexOf(rows[0], k);
                    return index >= 0 && index < rows[n].Length ? rows[n][index].Trim() : "";
                }
                var entry = new CostCenterOwnerImportRow(Get("costCenterCode"), Get("managerUsername"));
                try
                {
                    Guard.Require(seen.Add(entry.CostCenterCode), "duplicate",
                        "Centru de cost duplicat.", "Duplicate cost centre.");
                    await Validate(entry, db);
                    entries.Add(entry);
                }
                catch (DomainException error)
                {
                    errors.Add(new { row = n + 1, message = error.Message });
                }
            }
            return new { hash, rows = entries, errors, count = entries.Count };
        });

        root.MapPost("/import/confirm",
            async (CostCenterOwnerImportBatch input, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(db, c, input, async () =>
                {
                    Guard.Reason(input.Reason);
                    Guard.Require(input.Rows.Count is > 0 and <= 10000
                        && input.Rows.Select(x => x.CostCenterCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                            == input.Rows.Count,
                        "rows", "Rânduri invalide sau duplicate.", "Invalid or duplicate rows.");
                    var assignments = new List<(CostCenterOwnerImportRow Row, User Manager)>();
                    foreach (var entry in input.Rows)
                        assignments.Add((entry, await Validate(entry, db)));

                    foreach (var (entry, manager) in assignments)
                    {
                        var owner = await db.CostCenterOwners.FindAsync(entry.CostCenterCode);
                        var before = owner == null ? null : new { owner.ManagerId, owner.UpdatedAt, owner.UpdatedBy };
                        if (owner == null)
                        {
                            owner = new CostCenterOwner { CostCenterCode = entry.CostCenterCode };
                            db.CostCenterOwners.Add(owner);
                        }
                        owner.ManagerId = manager.Id;
                        owner.UpdatedAt = DateTime.UtcNow;
                        owner.UpdatedBy = Reports.Actor(c.User);
                        svc.Log(c.User, null, entry.CostCenterCode, "CostCenterOwnerImported", before,
                            new { owner.CostCenterCode, owner.ManagerId }, input.Reason);
                    }
                    // Flush under the atomic transaction so the route resolver reads
                    // the complete new mapping before updating pending approvals.
                    await db.SaveChangesAsync();
                    var changed = input.Rows.Select(x => x.CostCenterCode).ToHashSet();
                    var pending = await db.Reports.Where(r =>
                            r.State == States.Manager || r.State == States.Reapproval)
                        .Include(r => r.Lines).ToListAsync();
                    foreach (var report in pending.Where(r =>
                        r.Lines.Any(l => !l.Archived && changed.Contains(l.Data().CostCenter))))
                    {
                        var route = await CostCenterRouting.Preview(db, report);
                        if (!route.Ready || report.ManagerId == route.ManagerId) continue;
                        var prior = report.ManagerId;
                        report.ManagerId = route.ManagerId!;
                        svc.Snapshot(report, c.User);
                        Reports.Touch(report);
                        svc.Log(c.User, report.Id, "Report", "ApprovalOwnerRouted",
                            new { managerId = prior }, new { report.ManagerId, route.RoutingMode },
                            input.Reason);
                        await svc.Notify(report, report.ManagerId, "ReapprovalRequired");
                    }
                    return new { count = assignments.Count };
                })
        );
    }

    private static async Task<User> Validate(CostCenterOwnerImportRow entry, Db db)
    {
        Guard.Require(!string.IsNullOrWhiteSpace(entry.CostCenterCode)
            && !string.IsNullOrWhiteSpace(entry.ManagerUsername), "required",
            "Completează centrul de cost și managerul.", "Cost centre and manager are required.");
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var centers = await db.Masters.Where(m =>
            m.Kind == "costCenters" && m.Code == entry.CostCenterCode).ToListAsync();
        Guard.Require(centers.Any(m => Rules.Effective(m, date)), "cost_center",
            "Centrul de cost nu există sau nu este activ: " + entry.CostCenterCode,
            "Unknown or inactive cost centre: " + entry.CostCenterCode);
        var normalized = entry.ManagerUsername.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u =>
            u.NormalizedUserName == normalized && u.Active);
        Guard.Require(user != null && await db.UserRoles.Join(
            db.Roles, ur => ur.RoleId, role => role.Id,
            (ur, role) => new { ur.UserId, role.Name })
            .AnyAsync(x => x.UserId == user.Id && x.Name == "Manager"),
            "owner",
            "Manager inexistent, inactiv sau fără rol Manager: " + entry.ManagerUsername,
            "Missing or inactive Manager-role account: " + entry.ManagerUsername);
        return user!;
    }
}
