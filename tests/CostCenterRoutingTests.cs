using System.Security.Claims;
using Decont;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Decont.Tests;

public class CostCenterRoutingTests
{
    static Line Expense(Guid reportId, string centre, decimal ron, int position) =>
        new()
        {
            ReportId = reportId,
            Position = position,
            EstimatedRon = ron,
            Json = Serialization.Write(new ExpenseData(
                "Supplier", null, "R" + position, "2026-10-10", "DE",
                "Receipt", "GENERAL", "Travel", "Personal", null,
                centre, null, ron, "RON", [], false
            )),
        };

    static ClaimsPrincipal Employee(string id) =>
        new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Role, "Employee"),
        ], "unit"));

    static async Task<(Db Db, string Alice, string ManagerA, string ManagerB)> Setup(Factory factory)
    {
        await factory.Seed();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        // A separate scope is not disposed until after the test returns; Db
        // is scoped to the enclosing Factory and disposed with it.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var first = (await users.FindByNameAsync("manager"))!;
        var second = new User
        {
            UserName = "manager-two",
            Name = "Manager B",
            Email = "manager-two@example.invalid",
            Worker = "MANAGER-B",
        };
        Assert.True((await users.CreateAsync(second, "Valid-test-Password1!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(second, "Manager")).Succeeded);
        foreach (var code in new[] { "CC-A", "CC-B", "CC-C" })
            db.Masters.Add(new Master
            {
                Kind = "costCenters", Code = code,
                NameRo = code, NameEn = code, From = new(2020, 1, 1),
            });
        db.CostCenterOwners.AddRange(
            new CostCenterOwner { CostCenterCode = "CC-A", ManagerId = first.Id },
            new CostCenterOwner { CostCenterCode = "CC-B", ManagerId = second.Id }
        );
        await db.SaveChangesAsync();
        var alice = (await users.FindByNameAsync("alice"))!;
        return (db, alice.Id, first.Id, second.Id);
    }

    [Fact]
    public async Task LargestCombinedRonTotalWins_NotLargestSingleReceiptOrHeader()
    {
        await using var f = new Factory();
        var (db, alice, a, b) = await Setup(f);
        var report = new Report
        {
            EmployeeId = alice, CostCenter = "CC-A", ManagerId = a,
        };
        report.Lines.Add(Expense(report.Id, "CC-A", 80, 1));
        report.Lines.Add(Expense(report.Id, "CC-B", 45, 2));
        report.Lines.Add(Expense(report.Id, "CC-B", 55, 3));
        var route = await CostCenterRouting.Preview(db, report);
        Assert.True(route.Ready, route.MessageEn);
        Assert.Equal("CC-B", route.DominantCostCenter);
        Assert.Equal(100m, route.TotalRon);
        Assert.Equal(b, route.ManagerId);
        Assert.Equal(2, route.CostCenters.Count);

        var changed = await new Reports(db).RefreshApprovalManager(report, Employee(alice));
        Assert.True(changed);
        Assert.Equal(b, report.ManagerId);

        // A header change is NOT a bulk reallocation of the receipts.
        await new Reports(db).ConfigureCostCenter(report, "CC-C");
        Assert.Equal("CC-C", report.CostCenter);
        Assert.Equal(new[] { "CC-A", "CC-B", "CC-B" },
            report.Lines.Select(l => l.Data().CostCenter).ToArray());
        Assert.Equal(b, (await CostCenterRouting.Preview(db, report)).ManagerId);
    }

    [Fact]
    public async Task MissingOwnerInactiveOwnerAndCrossManagerTieBlockRouting()
    {
        await using var f = new Factory();
        var (db, alice, _, managerB) = await Setup(f);
        var report = new Report { EmployeeId = alice, CostCenter = "CC-A" };
        report.Lines.Add(Expense(report.Id, "CC-A", 80, 1));
        report.Lines.Add(Expense(report.Id, "CC-B", 80, 2));
        var tie = await CostCenterRouting.Preview(db, report);
        Assert.False(tie.Ready);
        Assert.Equal("approval_cost_center_tie", tie.ErrorCode);

        report.Lines.Add(Expense(report.Id, "CC-C", 10, 3));
        var missing = await CostCenterRouting.Preview(db, report);
        Assert.Equal("cost_center_owner_missing", missing.ErrorCode);
        Assert.Contains("CC-C", missing.MessageEn);

        report.Lines[2].Archived = true;
        var manager = await db.Users.SingleAsync(u => u.Id == managerB);
        manager.Active = false;
        await db.SaveChangesAsync();
        var inactive = await CostCenterRouting.Preview(db, report);
        Assert.Equal("cost_center_owner_missing", inactive.ErrorCode);
    }

    [Fact]
    public async Task FixedApproverOverridesCostCenterOwnerWithoutChangingLineAllocations()
    {
        await using var f = new Factory();
        var (db, alice, centreManager, namedManager) = await Setup(f);
        var employee = await db.Users.SingleAsync(u => u.Id == alice);
        employee.ApprovalMode = "Fixed";
        employee.FixedApproverId = namedManager;
        await db.SaveChangesAsync();

        var report = new Report { EmployeeId = alice, CostCenter = "CC-A" };
        report.Lines.Add(Expense(report.Id, "CC-A", 600, 1));
        report.Lines.Add(Expense(report.Id, "CC-B", 20, 2));
        var route = await CostCenterRouting.Preview(db, report);
        Assert.True(route.Ready, route.MessageEn);
        Assert.Equal("Fixed", route.RoutingMode);
        Assert.Equal(namedManager, route.ManagerId);
        Assert.Equal("CC-A", report.Lines[0].Data().CostCenter);
        Assert.Equal("CC-B", report.Lines[1].Data().CostCenter);
        Assert.NotEqual(centreManager, route.ManagerId);

        employee.FixedApproverId = null;
        await db.SaveChangesAsync();
        var missing = await CostCenterRouting.Preview(db, report);
        Assert.Equal("fixed_approver_invalid", missing.ErrorCode);

        employee.FixedApproverId = alice;
        await db.SaveChangesAsync();
        var self = await CostCenterRouting.Preview(db, report);
        Assert.Equal("fixed_approver_invalid", self.ErrorCode);
    }

    [Fact]
    public async Task EmployeeDbCostCenterOverridesLegacyPolicyAndIsIndependentFromReportHeader()
    {
        await using var f = new Factory();
        var (db, alice, _, _) = await Setup(f);
        var employee = await db.Users.SingleAsync(u => u.Id == alice);
        employee.CostCenter = "CC-B";
        await db.SaveChangesAsync();
        var report = new Report { EmployeeId = alice, CostCenter = "CC-A" };
        Assert.Equal("CC-B", await new Reports(db).EmployeeCostCenter(report));
        Assert.Equal("CC-A", report.CostCenter);
    }
}
