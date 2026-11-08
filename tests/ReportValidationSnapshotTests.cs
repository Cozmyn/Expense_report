using Decont;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Decont.Tests;

/// <summary>
/// Characterization tests for the existing report validation behaviour.
/// These tests intentionally preserve existing duplicate scope and VAT result codes.
/// </summary>
public class ReportValidationSnapshotTests
{
    static Db Database()
    {
        var db = new Db(new DbContextOptionsBuilder<Db>()
            .UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        return db;
    }

    [Fact]
    public async Task ValidationPreservesDuplicateFindingsAcrossRepeatedReads()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();

        var original = new ExpenseData(
            "Supplier", "18546290", "INV-EXISTING", "2026-01-01",
            "DE", "Receipt", "GENERAL", "Trip", "Personal", null, "CC", null,
            100m, "EUR", []);

        var first = new Report
        {
            Purpose = "First report",
            Lines = [new Line { Json = Serialization.Write(original), Position = 1, EstimatedRon = 100m }]
        };
        var second = new Report
        {
            Purpose = "Second report",
            Lines = [new Line { Json = Serialization.Write(original), Position = 1, EstimatedRon = 100m }]
        };
        db.Reports.AddRange(first, second);
        await db.SaveChangesAsync();

        var reports = new Reports(db);
        var before = await reports.Validate(first, erp: true);
        var after = await reports.Validate(first, erp: true);

        Assert.Contains(before, issue => issue.Code == "duplicate_exact" && issue.Blocking);
        Assert.Equal(
            before.Select(issue => (issue.Code, issue.Blocking, issue.LineId)).ToArray(),
            after.Select(issue => (issue.Code, issue.Blocking, issue.LineId)).ToArray());
    }
}
