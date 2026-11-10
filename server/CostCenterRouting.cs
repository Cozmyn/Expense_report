using Microsoft.EntityFrameworkCore;

namespace Decont;

/// <summary>
/// Authoritative, audited owner of a cost centre. Approval routing never falls
/// back to the employee's organisational manager.
/// </summary>
public class CostCenterOwner
{
    public string CostCenterCode { get; set; } = "";
    public string ManagerId { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public record CostCenterOwnerInput(string CostCenterCode, string ManagerId, string Reason);

public record CostCenterTotal(string Code, decimal TotalRon, string? ManagerId, string? ManagerName);

public record ApprovalRouting(
    string? ManagerId,
    string? ManagerName,
    string? DominantCostCenter,
    decimal TotalRon,
    List<CostCenterTotal> CostCenters,
    string? ErrorCode,
    string? MessageRo,
    string? MessageEn
)
{
    public bool Ready => ErrorCode == null;
}

public static class CostCenterRouting
{
    /// <summary>
    /// Total each cost centre in RON (Finance-confirmed when available, otherwise
    /// the recorded RON estimate). A tied maximum with different owners is
    /// ambiguous and must be corrected, never resolved arbitrarily.
    /// </summary>
    public static async Task<ApprovalRouting> Preview(Db db, Report report)
    {
        var lines = report.Lines.Where(l => !l.Archived).ToList();
        List<(string Code, decimal Total)> groups;
        if (lines.Count == 0)
            groups = string.IsNullOrWhiteSpace(report.CostCenter)
                ? []
                : [(report.CostCenter.Trim(), 0m)];
        else
            groups = lines.GroupBy(l => (l.Data().CostCenter ?? "").Trim(), StringComparer.Ordinal)
                .Select(g => (Code: g.Key, Total: Rules.Money(g.Sum(l => l.Final()))))
                .OrderBy(g => g.Code, StringComparer.Ordinal)
                .ToList();

        if (groups.Count == 0)
            return new(null, null, null, 0m, [],
                "cost_center_missing",
                "Alocă mai întâi un centru de cost angajatului sau selectează unul în antet.",
                "Assign an employee cost centre or choose one in the report header.");

        var owners = await db.CostCenterOwners.AsNoTracking().ToListAsync();
        var byCode = owners.ToDictionary(o => o.CostCenterCode, StringComparer.Ordinal);
        var ids = owners.Select(o => o.ManagerId).Distinct().ToList();
        var managerUsers = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Active)
            .ToDictionaryAsync(u => u.Id);
        var managerRoles = (await db.UserRoles.Join(
                db.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(x => x.Name == "Manager")
            .Select(x => x.UserId).ToListAsync()).ToHashSet();

        string? Owner(string code) =>
            byCode.TryGetValue(code, out var mapping)
            && managerUsers.ContainsKey(mapping.ManagerId)
            && managerRoles.Contains(mapping.ManagerId)
                ? mapping.ManagerId : null;

        var totals = groups.Select(g => new CostCenterTotal(
            g.Code, g.Total, Owner(g.Code),
            Owner(g.Code) is { } id ? managerUsers[id].Name : null
        )).ToList();
        var missing = totals.Where(t => t.ManagerId == null).Select(t => t.Code).ToList();
        if (missing.Count != 0)
        {
            var shown = string.Join(", ", missing.Select(c => c == "" ? "(fără centru de cost)" : c));
            return new(null, null, null, 0m, totals,
                "cost_center_owner_missing",
                "Lipsește un manager activ, cu rol Manager, responsabil pentru centrul de cost: " + shown + ". Configurează proprietarul în Admin.",
                "A valid active Manager owner is missing for cost centre: " + shown + ". Assign an owner in Admin.");
        }

        var max = totals.Max(x => x.TotalRon);
        var winners = totals.Where(t => t.TotalRon == max).OrderBy(t => t.Code, StringComparer.Ordinal).ToList();
        if (winners.Select(t => t.ManagerId).Distinct().Count() > 1)
            return new(null, null, null, max, totals,
                "approval_cost_center_tie",
                "Două sau mai multe centre de cost au aceeași sumă maximă, dar manageri diferiți. Verifică alocarea înainte de trimitere.",
                "Two or more cost centres have the same highest amount but different managers. Resolve the allocation before submission.");

        var selected = winners[0];
        if (selected.ManagerId == report.EmployeeId)
            return new(null, null, null, selected.TotalRon, totals,
                "self_approval",
                "Managerul centrului de cost nu își poate aproba propriul decont. Configurează un alt responsabil.",
                "The cost centre manager cannot approve their own expense report. Assign another owner.");

        return new(selected.ManagerId, selected.ManagerName, selected.Code,
            selected.TotalRon, totals, null, null, null);
    }

    public static void Map(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin/cost-center-owners")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        admin.MapGet(
            "",
            async (Db db) => await db.CostCenterOwners.AsNoTracking()
                .OrderBy(x => x.CostCenterCode).ToListAsync()
        );

        admin.MapPut(
            "/{code}",
            async (string code, CostCenterOwnerInput input, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(db, c, input, async () =>
                {
                    Guard.Reason(input.Reason);
                    var center = code.Trim();
                    Guard.Require(
                        center != "" && center == input.CostCenterCode.Trim(),
                        "cost_center", "Codul centrului de cost este invalid.", "Invalid cost centre code."
                    );
                    var day = DateOnly.FromDateTime(DateTime.UtcNow);
                    var masters = await db.Masters.Where(m =>
                        m.Kind == "costCenters" && m.Code == center).ToListAsync();
                    Guard.Require(
                        masters.Any(m => Rules.Effective(m, day)),
                        "cost_center", "Centrul de cost trebuie să fie activ.", "Cost centre must be active."
                    );
                    var manager = await db.Users.FindAsync(input.ManagerId);
                    Guard.Require(
                        manager != null && manager.Active
                            && await db.UserRoles.Join(db.Roles, r => r.RoleId, role => role.Id,
                                (r, role) => new { r.UserId, role.Name })
                                .AnyAsync(x => x.UserId == input.ManagerId && x.Name == "Manager"),
                        "owner", "Alege un manager activ cu rol Manager.", "Choose an active user with the Manager role."
                    );
                    var owner = await db.CostCenterOwners.FindAsync(center);
                    var before = owner == null ? null : new { owner.ManagerId, owner.UpdatedAt, owner.UpdatedBy };
                    if (owner == null)
                    {
                        owner = new CostCenterOwner { CostCenterCode = center };
                        db.CostCenterOwners.Add(owner);
                    }
                    owner.ManagerId = input.ManagerId;
                    owner.UpdatedAt = DateTime.UtcNow;
                    owner.UpdatedBy = Reports.Actor(c.User);
                    svc.Log(c.User, null, center, "CostCenterOwnerChanged", before,
                        new { owner.CostCenterCode, owner.ManagerId }, input.Reason);

                    // Preserve pending approvals when an owner changes, but route
                    // unresolved reports to the new owner with a new snapshot.
                    await db.SaveChangesAsync();
                    var pending = await db.Reports
                        .Where(r => r.State == States.Manager || r.State == States.Reapproval)
                        .Include(r => r.Lines).ToListAsync();
                    foreach (var report in pending.Where(r =>
                        r.Lines.Any(l => !l.Archived && l.Data().CostCenter == center)))
                    {
                        var route = await Preview(db, report);
                        if (!route.Ready || route.ManagerId == report.ManagerId) continue;
                        var oldManager = report.ManagerId;
                        report.ManagerId = route.ManagerId!;
                        svc.Snapshot(report, c.User);
                        Reports.Touch(report);
                        svc.Log(c.User, report.Id, "Report", "ApprovalOwnerRouted",
                            new { managerId = oldManager }, new { report.ManagerId, route.DominantCostCenter },
                            input.Reason);
                        await svc.Notify(report, report.ManagerId, "ReapprovalRequired");
                    }
                    return new { owner.CostCenterCode, owner.ManagerId, owner.UpdatedAt };
                })
        );
    }
}
