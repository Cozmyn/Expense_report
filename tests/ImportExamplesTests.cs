using Decont;
using Xunit;

namespace Decont.Tests;

public class ImportExamplesTests
{
    [Theory]
    [InlineData("employees", "approvalMode", "fixedApproverUsername")]
    [InlineData("managers", "role", "username")]
    [InlineData("costCenters", "erpCode", "from")]
    [InlineData("cards", "lastFour", "ownerUsername")]
    [InlineData("cost-center-owners", "managerUsername", "costCenterCode")]
    [InlineData("bank-statement", "settledRon", "reference")]
    [InlineData("generic", "json", "code")]
    public void DownloadableTemplatesProvideRequiredHeadersAndRealExampleRows(
        string kind, string requiredA, string requiredB)
    {
        var (headers, rows) = ImportExamples.For(kind);
        Assert.Contains(requiredA, headers);
        Assert.Contains(requiredB, headers);
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(headers.Length, row.Length));
        var contents = SpreadsheetImports.CsvText([headers, .. rows]);
        var parsed = Csv.Read(contents);
        Assert.Equal(headers, parsed[0]);
        Assert.Equal(rows.Length + 1, parsed.Count);
    }

    [Fact]
    public void CardTemplateContainsOnlyLastFourAndNeverFullPanOrCvv()
    {
        var (headers, rows) = ImportExamples.For("cards");
        Assert.Contains("lastFour", headers);
        Assert.DoesNotContain("cardNumber", headers);
        Assert.DoesNotContain("cvv", headers);
        var index = Array.IndexOf(headers, "lastFour");
        Assert.All(rows, row =>
        {
            Assert.Equal(4, row[index].Length);
            Assert.True(row[index].All(char.IsAsciiDigit));
        });
    }

    [Fact]
    public void EmployeeExampleDifferentiatesNamedApprovalExceptionFromCostCentreRouting()
    {
        var (headers, rows) = ImportExamples.For("employees");
        var mode = Array.IndexOf(headers, "approvalMode");
        var fixedName = Array.IndexOf(headers, "fixedApproverUsername");
        var role = Array.IndexOf(headers, "role");
        Assert.Contains(rows, r => r[mode] == "Fixed" && r[fixedName].Length > 0);
        Assert.Contains(rows, r => r[mode] == "CostCenter" && r[fixedName] == "");
        Assert.All(rows, r => Assert.Equal("Employee", r[role]));
    }
}
