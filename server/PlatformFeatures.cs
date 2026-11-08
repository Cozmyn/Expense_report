using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class PlatformFeatures
{
    public static void AccountingPermission(
        ClaimsPrincipal user,
        ExpenseData data,
        string? reason,
        AccountingOverride? existing = null
    )
    {
        if (data.Accounting == existing)
            return;
        Guard.Require(
            user.IsInRole("Finance"),
            "accounting_permission",
            "Doar Finance poate modifica maparea contabilă.",
            "Only Finance can override accounting mappings.",
            403
        );
        Guard.Reason(reason);
        if (data.Accounting != null)
            Guard.Require(
                data.Accounting.MainAccount.Trim() != "" && data.Accounting.LocalAccount.Trim() != "",
                "accounting",
                "Contul GL și contul local sunt obligatorii.",
                "GL and local account are required."
            );
    }

    public static async Task<bool> Active(Db db, User user, DateOnly day)
    {
        if (!user.Active)
            return false;
        var assignment = await db
            .Assignments.Where(a => a.UserId == user.Id && a.From <= day && (a.To == null || a.To >= day))
            .SingleOrDefaultAsync();
        return assignment?.Active ?? true;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
            "/preferences/{key}",
            async (string key, HttpContext c, Db db) =>
            {
                var row = await db.Preferences.SingleOrDefaultAsync(p =>
                    p.UserId == Reports.Actor(c.User) && p.Key == key
                );
                return Results.Content(row?.Json ?? "{}", "application/json");
            }
        );
        api.MapPut(
            "/preferences/{key}",
            async (string key, JsonElement json, HttpContext c, Db db, Reports svc) =>
                await Api.Atomic(
                    db,
                    c,
                    json,
                    async () =>
                    {
                        Guard.Require(
                            key.Length <= 80 && json.GetRawText().Length <= 32000,
                            "size",
                            "Preferințe prea mari.",
                            "Preferences are too large."
                        );
                        var id = Serialization.Hash(Reports.Actor(c.User) + ":" + key);
                        var row = await db.Preferences.FindAsync(id);
                        if (row == null)
                        {
                            row = new()
                            {
                                Id = id,
                                UserId = Reports.Actor(c.User),
                                Key = key,
                            };
                            db.Preferences.Add(row);
                        }
                        row.Json = json.GetRawText();
                        return new { saved = true };
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/comments",
            async (Guid id, CommentInput i, HttpContext c, Db db, Reports svc) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        Guard.Require(
                            await svc.Manager(r, c.User) || c.User.IsInRole("Finance"),
                            "permission",
                            "Doar managerul sau Finance poate comenta.",
                            "Only the assigned manager or Finance can comment.",
                            403
                        );
                        Guard.Require(
                            i.Text.Trim().Length is >= 3 and <= 2000
                                && (i.LineId == null || r.Lines.Any(l => l.Id == i.LineId && !l.Archived)),
                            "comment",
                            "Comentariu sau bon invalid.",
                            "Invalid comment or receipt."
                        );
                        var comment = new LineComment
                        {
                            ReportId = id,
                            LineId = i.LineId,
                            Revision = r.Revision,
                            Actor = Reports.Actor(c.User),
                            Text = i.Text.Trim(),
                        };
                        db.Comments.Add(comment);
                        svc.Log(c.User, id, i.LineId?.ToString() ?? "Report", "CommentAdded", after: comment);
                        return comment;
                    }
                )
        );
        var admin = api.MapGroup("/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet(
            "/assignments",
            async (Db db) => await db.Assignments.OrderByDescending(a => a.From).ToListAsync()
        );
        admin.MapPost(
            "/assignments",
            async (AssignmentInput i, HttpContext c, Db db, Reports svc, UserManager<User> users) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        Guard.Reason(i.Reason);
                        Guard.Require(
                            i.From <= (i.To ?? DateOnly.MaxValue) && i.UserId != i.ManagerId,
                            "dates",
                            "Alocare invalidă.",
                            "Invalid assignment."
                        );
                        var u = await users.FindByIdAsync(i.UserId);
                        var m = await users.FindByIdAsync(i.ManagerId);
                        Guard.Require(
                            u != null && m != null && m.Active && await users.IsInRoleAsync(m, "Manager"),
                            "user",
                            "Utilizator sau manager invalid.",
                            "Invalid user or manager."
                        );
                        Guard.Require(
                            !await db.Assignments.AnyAsync(a =>
                                a.UserId == i.UserId
                                && a.From <= (i.To ?? DateOnly.MaxValue)
                                && (a.To == null || a.To >= i.From)
                            ),
                            "overlap",
                            "Închide perioada anterioară.",
                            "Close the preceding assignment."
                        );
                        if (i.OrganizationCode != "")
                            Guard.Require(
                                await db.Masters.AnyAsync(a =>
                                    a.Kind == "organizationUnits" && a.Code == i.OrganizationCode && a.Active
                                ),
                                "organization",
                                "Unitate organizațională invalidă.",
                                "Invalid organization unit."
                            );
                        var a = new Assignment
                        {
                            UserId = i.UserId,
                            ManagerId = i.ManagerId,
                            OrganizationCode = i.OrganizationCode,
                            Active = i.Active,
                            From = i.From,
                            To = i.To,
                        };
                        db.Assignments.Add(a);
                        svc.Log(c.User, null, a.Id.ToString(), "AssignmentCreated", after: a, reason: i.Reason);
                        return a;
                    }
                )
        );
        admin.MapPost(
            "/assignments/{id:guid}/retire",
            async (Guid id, RetireInput i, HttpContext c, Db db, Reports svc) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        Guard.Reason(i.Reason);
                        var a =
                            await db.Assignments.FindAsync(id)
                            ?? throw new DomainException(
                                "missing",
                                "Alocare inexistentă.",
                                "Assignment not found.",
                                404
                            );
                        Guard.Require(
                            i.To >= a.From && (a.To == null || i.To <= a.To),
                            "date",
                            "Perioadă invalidă.",
                            "Invalid effective period."
                        );
                        var before = Serialization.Write(a);
                        a.To = i.To;
                        svc.Log(c.User, null, id.ToString(), "AssignmentRetired", before, a, i.Reason);
                        return a;
                    }
                )
        );
        api.MapGet(
                "/finance/queue",
                async (Reports svc, Db db, HttpContext c) =>
                {
                    var reports = await svc.Scope(c.User)
                        .Where(r => r.State != States.Draft)
                        .Include(r => r.Attachments)
                        .Include(r => r.Lines)
                            .ThenInclude(l => l.Evidence)
                        .ToListAsync();
                    var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
                    var result = new List<object>();
                    foreach (var r in reports)
                    {
                        var issues = await svc.Validate(r, true);
                        result.Add(
                            new
                            {
                                report = Api.View(r),
                                employee = users.GetValueOrDefault(r.EmployeeId),
                                manager = users.GetValueOrDefault(r.ManagerId),
                                ageDays = (int)(DateTime.UtcNow - r.Created).TotalDays,
                                issues,
                                exceptionCodes = issues.Select(p => p.Code).Distinct(),
                            }
                        );
                    }
                    return result;
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance", "Auditor"));
        api.MapGet(
            "/reporting",
            async (Reports svc, Db db, HttpContext c) =>
            {
                var rows = await svc.Scope(c.User).Include(r => r.Lines).ToListAsync();
                var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name);
                var masters = await db.Masters.ToListAsync();
                return rows.SelectMany(r =>
                    r.Lines.Where(l => !l.Archived)
                        .Select(l =>
                        {
                            var d = l.Data();
                            var m = masters.FirstOrDefault(m =>
                                m.Kind == "accounting"
                                && m.Code == d.Category
                                && Rules.Effective(m, DateOnly.Parse(d.Date))
                            );
                            return new
                            {
                                reportId = r.Id,
                                r.Number,
                                r.State,
                                employeeId = r.EmployeeId,
                                employee = users.GetValueOrDefault(r.EmployeeId),
                                r.ManagerId,
                                lineId = l.Id,
                                d.Date,
                                month = d.Date[..7],
                                d.Category,
                                d.Supplier,
                                d.Cui,
                                d.CostCenter,
                                d.Project,
                                d.CardId,
                                d.Currency,
                                d.Amount,
                                finalRon = l.Final(),
                                gl = d.Accounting?.MainAccount ?? (m == null ? "" : Rules.Text(m, "mainAccount")),
                                vatCodeCount = Rules.VatCodeCount(
                                    d,
                                    masters,
                                    d.TaxCurrency == "RON" ? 1m
                                        : d.Amount > 0 ? l.Final() / d.Amount
                                        : null
                                ),
                            };
                        })
                );
            }
        );
        api.MapGet(
                "/audit/export",
                async (Db db, HttpContext c) =>
                {
                    var q = AuditQuery(db, c);
                    var rows = await q.OrderBy(a => a.At).Take(50000).ToListAsync();
                    var csv =
                        "Time,Actor,Report,Entity,Action,Before,After,Reason\r\n"
                        + string.Join(
                            "\r\n",
                            rows.Select(a =>
                                string.Join(
                                    ",",
                                    new object?[]
                                    {
                                        a.At.ToString("O"),
                                        a.Actor,
                                        a.ReportId,
                                        a.Entity,
                                        a.Action,
                                        a.Before,
                                        a.After,
                                        a.Reason,
                                    }.Select(Csv.Cell)
                                )
                            )
                        );
                    return Results.File(Encoding.UTF8.GetBytes("\uFEFF" + csv), "text/csv", "audit.csv");
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance", "Admin", "Auditor"));
    }

    public static IQueryable<Audit> AuditQuery(Db db, HttpContext c)
    {
        var q = db.Audits.AsQueryable();
        var search = c.Request.Query["q"].ToString();
        var actor = c.Request.Query["actor"].ToString();
        var action = c.Request.Query["action"].ToString();
        if (search != "")
            q = q.Where(a => a.Entity.Contains(search) || (a.Reason != null && a.Reason.Contains(search)));
        if (actor != "")
            q = q.Where(a => a.Actor == actor);
        if (action != "")
            q = q.Where(a => a.Action == action);
        if (DateTime.TryParse(c.Request.Query["from"], out var from))
            q = q.Where(a => a.At >= from);
        if (DateTime.TryParse(c.Request.Query["to"], out var to))
        {
            to = to.Date.AddDays(1);
            q = q.Where(a => a.At < to);
        }
        return q;
    }
}

public record AssignmentInput(
    string UserId,
    string ManagerId,
    string OrganizationCode,
    bool Active,
    DateOnly From,
    DateOnly? To,
    string Reason
);
