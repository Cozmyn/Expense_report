using Decont;
using Xunit;
namespace Decont.Tests;
public class OrganisationImportTests {
 [Fact]public void ImportChecksCyclesAcrossSeveralNewRows(){var rows=new List<Assignment>{new(){UserId="a",ManagerId="b",From=new(2026,1,1)},new(){UserId="b",ManagerId="c",From=new(2026,1,1)},new(){UserId="c",ManagerId="a",From=new(2027,1,1)}};Assert.Equal("manager_cycle",Assert.Throws<DomainException>(()=>OrganisationImports.ValidateSchedule(rows)).Code);}
 [Fact]public void ImportRejectsOverlappingPeriodsEvenWhenOneIsInactive(){var rows=new List<Assignment>{new(){UserId="a",ManagerId="b",From=new(2026,1,1),To=new(2026,2,1)},new(){UserId="a",ManagerId="c",From=new(2026,2,1),Active=false}};Assert.Equal("overlap",Assert.Throws<DomainException>(()=>OrganisationImports.ValidateSchedule(rows)).Code);}
 [Fact]public void AdjacentPeriodsAndFutureManagerChangesAreValid(){OrganisationImports.ValidateSchedule([new(){UserId="a",ManagerId="b",From=new(2026,1,1),To=new(2026,1,31)},new(){UserId="a",ManagerId="c",From=new(2026,2,1)},new(){UserId="b",ManagerId="c",From=new(2026,1,1)}]);}
 [Fact]public void EmployeeCannotBeTheirOwnManager(){Assert.Equal("manager_cycle",Assert.Throws<DomainException>(()=>OrganisationImports.ValidateSchedule([new(){UserId="a",ManagerId="a",From=new(2026,1,1)}])).Code);}
 [Fact]public void InvalidDateRangeCannotBeImported(){Assert.Equal("dates",Assert.Throws<DomainException>(()=>OrganisationImports.ValidateSchedule([new(){UserId="a",ManagerId="b",From=new(2026,2,1),To=new(2026,1,1)}])).Code);}
}
