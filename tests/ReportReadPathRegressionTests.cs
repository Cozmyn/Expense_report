using Decont;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Decont.Tests;

/// <summary>
/// Characterization tests for the original duplicate-detection rules.
/// These protect the existing Finance semantics while the read path is optimized.
/// </summary>
public class ReportReadPathRegressionTests
{
    static Db Database()
    {
        var db = new Db(new DbContextOptionsBuilder<Db>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection();
        return db;
    }

    static ExpenseData Receipt(string invoice = "INVOICE-A", string supplier = "Supplier", decimal amount = 100m) =>
        new(supplier, "18546290", invoice, "2026-01-01", "DE", "Receipt",
            "GENERAL", "Business travel", "Personal", null, "CC", null, amount, "EUR", []);

    static Report ReportWith(ExpenseData data, string employee = "employee", bool archived = false, string? disposition = null)
    {
        var report = new Report
        {
            EmployeeId = employee,
            Purpose = "Report test",
            Lines = [new Line
            {
                Position = 1,
                Archived = archived,
                DuplicateDisposition = disposition,
                EstimatedRon = data.Amount,
                Json = Serialization.Write(data)
            }]
        };
        report.Lines[0].ReportId = report.Id;
        return report;
    }

    static async Task<List<Problem>> Validate(Db db, Report report) =>
        await new Reports(db).Validate(report, erp: true);

    static Problem[] Duplicates(IEnumerable<Problem> issues) =>
        issues.Where(p => p.Code is "duplicate_exact" or "duplicate_probable").ToArray();

    [Fact]
    public async Task ExactDuplicateStillChecksOtherEmployeesDraftsAndIsBlocking()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt(), "first");
        var anotherDraft = ReportWith(Receipt(supplier: "Completely unrelated"), "second");
        db.Reports.AddRange(target, anotherDraft);
        await db.SaveChangesAsync();

        var duplicate = Assert.Single(Duplicates(await Validate(db, target)));
        Assert.Equal("duplicate_exact", duplicate.Code);
        Assert.True(duplicate.Blocking);
        Assert.Equal(target.Lines[0].Id, duplicate.LineId);
    }

    [Fact]
    public async Task ProbableDuplicateUsesCaseInsensitiveSupplierButExactDateAndAmount()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt("INV-A", "SUPPLIER"));
        var probable = ReportWith(Receipt("INV-B", "supplier"));
        db.Reports.AddRange(target, probable);
        await db.SaveChangesAsync();

        Assert.Equal("duplicate_probable", Assert.Single(Duplicates(await Validate(db, target))).Code);
    }

    [Fact]
    public async Task ArchivedOtherLinesAndResolvedTargetAreExcludedFromWarnings()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt());
        var archived = ReportWith(Receipt(), archived: true);
        db.Reports.AddRange(target, archived);
        await db.SaveChangesAsync();

        Assert.Empty(Duplicates(await Validate(db, target)));
        archived.Lines[0].Archived = false;
        target.Lines[0].DuplicateDisposition = "FinanceReviewed";
        await db.SaveChangesAsync();
        Assert.Empty(Duplicates(await Validate(db, target)));
    }

    [Fact]
    public async Task ExactHashMatchAcrossReceiptEvidenceStillWinsOverNonmatchingInvoice()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt("TARGET"));
        var other = ReportWith(Receipt("OTHER", "Other supplier", 211m));
        target.Lines[0].Evidence.Add(new Evidence { Hash = "same-document", Scan = "Clean" });
        other.Lines[0].Evidence.Add(new Evidence { Hash = "same-document", Scan = "Clean" });
        db.Reports.AddRange(target, other);
        await db.SaveChangesAsync();

        Assert.Equal("duplicate_exact", Assert.Single(Duplicates(await Validate(db, target))).Code);
    }

    [Fact]
    public async Task OtherReportBundleHashStillMatchesTargetEvidence()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt("TARGET"));
        var other = ReportWith(Receipt("OTHER", "Different supplier", 211m));
        target.Lines[0].Evidence.Add(new Evidence { Hash = "shared-bundle", Scan = "Clean" });
        other.Attachments.Add(new ReportAttachment { Hash = "shared-bundle", Scan = "Clean" });
        db.Reports.AddRange(target, other);
        await db.SaveChangesAsync();

        Assert.Equal("duplicate_exact", Assert.Single(Duplicates(await Validate(db, target))).Code);
    }

    [Fact]
    public async Task CoveredCombinedFileStillMatchesEvidenceFromAnotherReport()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt("TARGET"));
        var other = ReportWith(Receipt("OTHER", "Another vendor", 211m));
        target.Attachments.Add(new ReportAttachment
        {
            Hash = "covered-hash",
            Scan = "Clean",
            CoveredLines = Serialization.Write(new[] { target.Lines[0].Id })
        });
        other.Lines[0].Evidence.Add(new Evidence { Hash = "covered-hash", Scan = "Clean" });
        db.Reports.AddRange(target, other);
        await db.SaveChangesAsync();

        Assert.Equal("duplicate_exact", Assert.Single(Duplicates(await Validate(db, target))).Code);
    }

    [Fact]
    public async Task UnrelatedReportsAreNotMarkedAsDuplicateAndRepeatReadIsStable()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var target = ReportWith(Receipt("TARGET"));
        var other = ReportWith(Receipt("OTHER", "Unrelated supplier", 201m));
        db.Reports.AddRange(target, other);
        await db.SaveChangesAsync();

        var first = Duplicates(await Validate(db, target));
        var second = Duplicates(await Validate(db, target));
        Assert.Empty(first);
        Assert.Equal(first.Select(x => x.Code).ToArray(), second.Select(x => x.Code).ToArray());
    }
}
