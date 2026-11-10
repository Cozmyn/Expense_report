using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record EmployeeRow(
    string Username,
    string Name,
    string Email,
    string Worker,
    string? ManagerUsername,
    bool Active,
    string? CostCenter = null,
    string Role = "Employee",
    string ApprovalMode = "CostCenter",
    string? FixedApproverUsername = null
);

public record EmployeeBatch(List<EmployeeRow> Rows, string Reason, string TemporaryPassword);

public static class EmployeeImports
{
    public static async Task Validate(EmployeeRow row, UserManager<User> users, List<EmployeeRow> prior, Db? db = null)
    {
        Guard.Require(
            row.Username.Length is >= 3 and <= 100
                && !string.IsNullOrWhiteSpace(row.Name)
                && !string.IsNullOrWhiteSpace(row.Worker)
                && System.Net.Mail.MailAddress.TryCreate(row.Email, out _),
            "user",
            "Utilizator, nume, Worker sau email invalid.",
            "Invalid username, name, Worker or email."
        );
        Guard.Require(
            await users.FindByNameAsync(row.Username) == null
                && await users.FindByEmailAsync(row.Email) == null
                && !prior.Any(r =>
                    r.Username.Equals(row.Username, StringComparison.OrdinalIgnoreCase)
                    || r.Email.Equals(row.Email, StringComparison.OrdinalIgnoreCase)
                ),
            "duplicate",
            "Utilizator sau email duplicat.",
            "Duplicate username or email."
        );
        if (!string.IsNullOrWhiteSpace(row.CostCenter) && db != null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var options = await db.Masters.Where(m => m.Kind == "costCenters" && m.Code == row.CostCenter.Trim())
                .ToListAsync();
            Guard.Require(
                options.Any(m => Rules.Effective(m, today)),
                "cost_center",
                "Centrul de cost al angajatului trebuie să existe și să fie activ.",
                "Employee cost centre must exist and be active."
            );
        }
        Guard.Require(row.Role is "Employee" or "Manager",
            "role", "Importă numai angajați sau manageri.", "Import only employees or managers.");
        Guard.Require(row.ApprovalMode is "CostCenter" or "Fixed",
            "approval_mode", "Mod de aprobare invalid.", "Invalid approval mode.");
        if (row.ApprovalMode == "Fixed")
        {
            Guard.Require(!string.IsNullOrWhiteSpace(row.FixedApproverUsername)
                && !row.FixedApproverUsername.Equals(row.Username, StringComparison.OrdinalIgnoreCase),
                "fixed_approver", "Excepția necesită un alt utilizator, manager activ.",
                "Fixed approval requires a different active Manager.");
            var fixedManager = await users.FindByNameAsync(row.FixedApproverUsername!);
            Guard.Require(fixedManager != null && fixedManager.Active
                && await users.IsInRoleAsync(fixedManager, "Manager"),
                "fixed_approver", "Managerul fix trebuie să existe și să fie activ.",
                "Fixed approver must already exist and be active.");
        }
        if (!string.IsNullOrWhiteSpace(row.ManagerUsername))
        {
            var manager = await users.FindByNameAsync(row.ManagerUsername);
            Guard.Require(
                manager != null && manager.Active && await users.IsInRoleAsync(manager, "Manager"),
                "manager",
                "Managerul trebuie să existe și să fie activ.",
                "Manager must already exist and be active."
            );
        }
    }

    public static void Map(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin/employee-import").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/template", () => ImportExamples.Download("employees"));
        admin.MapGet("/template/{kind}", (string kind) =>
            kind is "employees" or "managers"
                ? ImportExamples.Download(kind)
                : Results.NotFound());
        admin.MapPost(
            "/preview",
            async (HttpContext c, UserManager<User> users, Db db) =>
            {
                var (rows, hash) = await SpreadsheetImports.Upload(c);
                Guard.Require(rows.Count > 1, "rows", "Import gol.", "Empty import.");
                List<EmployeeRow> valid = [];
                List<object> errors = [];
                for (int n = 1; n < rows.Count; n++)
                {
                    try
                    {
                        string Get(string k, string fallback = "")
                        {
                            var index = Array.IndexOf(rows[0], k);
                            var value = index >= 0 && index < rows[n].Length ? rows[n][index] : "";
                            return value == "" ? fallback : value;
                        }
                        var row = new EmployeeRow(
                            Get("username"),
                            Get("name"),
                            Get("email"),
                            Get("worker"),
                            Get("managerUsername"),
                            Get("active") != "false",
                            Get("costCenter"),
                            Get("role", "Employee"),
                            Get("approvalMode", "CostCenter"),
                            Get("fixedApproverUsername")
                        );
                        await Validate(row, users, valid, db);
                        valid.Add(row);
                    }
                    catch (DomainException ex)
                    {
                        errors.Add(new { row = n + 1, message = ex.Message });
                    }
                }
                return new
                {
                    hash,
                    rows = valid,
                    errors,
                    count = valid.Count,
                };
            }
        );
        admin.MapPost(
            "/confirm",
            async (EmployeeBatch input, UserManager<User> users, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    new
                    {
                        input.Rows,
                        input.Reason,
                        passwordHash = Serialization.Hash(input.TemporaryPassword),
                    },
                    async () =>
                    {
                        Guard.Reason(input.Reason);
                        Guard.Require(
                            input.Rows.Count is > 0 and <= 10000,
                            "rows",
                            "Import gol sau prea mare.",
                            "Empty or oversized import."
                        );
                        List<EmployeeRow> prior = [];
                        foreach (var row in input.Rows)
                        {
                            await Validate(row, users, prior, db);
                            prior.Add(row);
                        }
                        List<object> created = [];
                        foreach (var row in input.Rows)
                        {
                            var manager = string.IsNullOrEmpty(row.ManagerUsername)
                                ? null
                                : await users.FindByNameAsync(row.ManagerUsername);
                            var fixedApprover = row.ApprovalMode == "Fixed"
                                ? await users.FindByNameAsync(row.FixedApproverUsername!)
                                : null;
                            var user = new User
                            {
                                UserName = row.Username,
                                Name = row.Name,
                                Email = row.Email,
                                Worker = row.Worker,
                                CostCenter = (row.CostCenter ?? "").Trim(),
                                ApprovalMode = row.ApprovalMode,
                                FixedApproverId = fixedApprover?.Id,
                                ManagerId = manager?.Id,
                                Active = row.Active,
                                ChangePassword = true,
                            };
                            var password = input.TemporaryPassword;
                            var result = await users.CreateAsync(user, password);
                            Guard.Require(
                                result.Succeeded,
                                "identity",
                                "Importul nu respectă politica conturilor.",
                                "Import violates account policy."
                            );
                            result = await users.AddToRoleAsync(user, row.Role);
                            Guard.Require(
                                result.Succeeded,
                                "identity",
                                "Rolul de import lipsește.",
                                "Import role is unavailable."
                            );
                            svc.Log(
                                c.User,
                                null,
                                user.Id,
                                "EmployeeImported",
                                after: new
                                {
                                    row.Username,
                                    row.Name,
                                    row.Worker,
                                    row.CostCenter,
                                    row.Role,
                                    row.ApprovalMode,
                                    fixedApproverId = fixedApprover?.Id,
                                    managerId = manager?.Id,
                                },
                                reason: input.Reason
                            );
                            created.Add(new { row.Username, user.Id });
                        }
                        return new { count = input.Rows.Count, created };
                    }
                )
        );
    }
}
