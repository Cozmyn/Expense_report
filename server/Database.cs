using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public class Db(DbContextOptions<Db> options) : IdentityDbContext<User>(options)
{
    public DbSet<PostingLineRecord> PostingLines => Set<PostingLineRecord>();
    public DbSet<PostingReview> PostingReviews => Set<PostingReview>();
    public DbSet<FinanceAcceptanceRecord> FinanceAcceptanceRecords => Set<FinanceAcceptanceRecord>();
    public DbSet<BankTreatmentRevocation> BankTreatmentRevocations => Set<BankTreatmentRevocation>();
    public DbSet<ReportReset> ReportResets => Set<ReportReset>();
    public DbSet<ReportRestore> ReportRestores => Set<ReportRestore>();
    public DbSet<BankTreatment> BankTreatments => Set<BankTreatment>();
    public DbSet<VerifiedVatComponent> VerifiedVatComponents => Set<VerifiedVatComponent>();
    public DbSet<TaxPolicyDraft> TaxPolicies => Set<TaxPolicyDraft>();
    public DbSet<TaxPolicyDecision> TaxPolicyDecisions => Set<TaxPolicyDecision>();
    public DbSet<FinancePolicyVersion> FinancePolicies => Set<FinancePolicyVersion>();
    public DbSet<ExpenseFxProposal> FxProposals => Set<ExpenseFxProposal>();
    public DbSet<ExpenseFxDecision> FxDecisions => Set<ExpenseFxDecision>();
    public DbSet<ExpenseCalculation> Calculations => Set<ExpenseCalculation>();
    public DbSet<DocumentEvent> DocumentEvents => Set<DocumentEvent>();
    public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();
    public DbSet<DocumentGroup> DocumentGroups => Set<DocumentGroup>();
    public DbSet<ExtractionAttempt> ExtractionAttempts => Set<ExtractionAttempt>();
    public DbSet<SupplierAccount> SupplierAccounts => Set<SupplierAccount>();
    public DbSet<Preference> Preferences => Set<Preference>();
    public DbSet<LineComment> Comments => Set<LineComment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<SchemaRelease> SchemaReleases => Set<SchemaRelease>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<CostCenterOwner> CostCenterOwners => Set<CostCenterOwner>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Evidence> Evidence => Set<Evidence>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<Master> Masters => Set<Master>();
    public DbSet<VendorRequest> Vendors => Set<VendorRequest>();
    public DbSet<CardTransaction> Transactions => Set<CardTransaction>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Idempotency> Idempotencies => Set<Idempotency>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<PostingLineRecord>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Posting_Money",
                    "\"DebitMinor\" >= 0 AND \"CreditMinor\" >= 0 AND NOT (\"DebitMinor\" > 0 AND \"CreditMinor\" > 0)"
                );
                t.HasCheckConstraint("CK_Posting_Sequence", "\"Sequence\" > 0");
            });
        b.Entity<PostingLineRecord>()
            .HasIndex(x => new { x.BatchId, x.ExpenseId })
            .IsUnique()
            .HasFilter("\"ExpenseSummary\" = TRUE");
        b.Entity<PostingLineRecord>().HasIndex(x => new { x.BatchId, x.Sequence }).IsUnique();
        b.Entity<PostingLineRecord>().HasIndex(x => new { x.BatchId, x.StableId }).IsUnique();
        b.Entity<PostingLineRecord>()
            .HasOne<Batch>()
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingLineRecord>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingLineRecord>()
            .HasOne<ExpenseCalculation>()
            .WithMany()
            .HasForeignKey(x => x.CalculationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingLineRecord>()
            .HasOne<ExpenseFxDecision>()
            .WithMany()
            .HasForeignKey(x => x.FxDecisionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingLineRecord>()
            .HasOne<VerifiedVatComponent>()
            .WithMany()
            .HasForeignKey(x => x.SourceVatComponentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingLineRecord>()
            .HasOne<Master>()
            .WithMany()
            .HasForeignKey(x => x.MappingId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingReview>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PostingReview>().HasIndex(x => new { x.ReportId, x.Revision });
        b.Entity<Batch>().HasIndex(x => x.BusinessKey).IsUnique().HasFilter("\"BusinessKey\" <> ''");
        b.Entity<ReportReset>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReportRestore>()
            .HasOne<ReportReset>()
            .WithMany()
            .HasForeignKey(x => x.ResetId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReportRestore>().HasIndex(x => x.ResetId).IsUnique();
        b.Entity<BankTreatment>()
            .HasOne<CardTransaction>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<BankTreatment>().HasIndex(x => x.TransactionId);
        b.Entity<BankTreatmentRevocation>().HasIndex(x => x.TreatmentId).IsUnique();
        b.Entity<BankTreatmentRevocation>()
            .HasOne<BankTreatment>()
            .WithMany()
            .HasForeignKey(x => x.TreatmentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<VerifiedVatComponent>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<VerifiedVatComponent>().HasIndex(x => new { x.CalculationId, x.RateBps }).IsUnique();
        b.Entity<VerifiedVatComponent>()
            .HasOne<ExpenseCalculation>()
            .WithMany()
            .HasForeignKey(x => x.CalculationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaxPolicyDecision>()
            .HasOne<TaxPolicyDraft>()
            .WithMany()
            .HasForeignKey(x => x.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<TaxPolicyDecision>().HasIndex(x => new { x.PolicyId, x.Action }).IsUnique();
        b.Entity<FinancePolicyVersion>().HasIndex(x => new { x.Currency, x.Version }).IsUnique();
        b.Entity<ExpenseFxProposal>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExpenseFxDecision>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExpenseCalculation>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExpenseCalculation>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentEvent>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(e => e.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentEvent>()
            .HasIndex(e => new
            {
                e.ReportId,
                e.DocumentId,
                e.Created,
            });
        b.Entity<Job>().Property(j => j.Attempts).IsConcurrencyToken();
        b.Entity<DocumentReview>()
            .HasOne<ReportAttachment>()
            .WithMany()
            .HasForeignKey(r => r.Id)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentReview>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(r => r.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentGroup>()
            .HasOne<DocumentReview>()
            .WithMany()
            .HasForeignKey(g => g.BundleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentGroup>()
            .HasOne<Line>()
            .WithMany()
            .HasForeignKey(g => g.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentGroup>().HasIndex(g => new { g.BundleId, g.SourceKey }).IsUnique();
        b.Entity<DocumentGroup>().HasIndex(g => g.ExpenseId).IsUnique();
        b.Entity<ExtractionAttempt>()
            .HasOne<DocumentReview>()
            .WithMany()
            .HasForeignKey(a => a.BundleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExtractionAttempt>().HasIndex(a => new { a.BundleId, a.Fingerprint }).IsUnique();
        b.Entity<SupplierAccount>().HasIndex(a => new { a.Cui, a.From }).IsUnique();
        b.Entity<Preference>().HasIndex(x => new { x.UserId, x.Key }).IsUnique();
        b.Entity<Assignment>().HasIndex(x => new { x.UserId, x.From });
        b.Entity<LineComment>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<CostCenterOwner>().HasKey(o => o.CostCenterCode);
        b.Entity<CostCenterOwner>()
            .HasOne<User>().WithMany()
            .HasForeignKey(o => o.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Report>()
            .HasMany(r => r.Attachments)
            .WithOne()
            .HasForeignKey(a => a.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Report>().HasIndex(r => r.Number).IsUnique();
        b.Entity<Report>().Property(r => r.Version).IsConcurrencyToken();
        b.Entity<Report>()
            .HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Line>()
            .HasMany(l => l.Evidence)
            .WithOne()
            .HasForeignKey(a => a.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Snapshot>().HasIndex(s => new { s.ReportId, s.Revision }).IsUnique();
        b.Entity<Master>()
            .HasIndex(m => new
            {
                m.Kind,
                m.Code,
                m.From,
            })
            .IsUnique();
        b.Entity<VendorRequest>().HasIndex(v => new { v.ReportId, v.Cui }).IsUnique();
        b.Entity<CardTransaction>().HasIndex(t => new { t.CardId, t.Reference }).IsUnique();
        b.Entity<Batch>().HasIndex(e => new { e.ReportId, e.Cycle }).IsUnique();
        b.Entity<Idempotency>().HasKey(x => x.Id);
        b.Entity<Snapshot>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Decision>()
            .HasOne<Report>()
            .WithMany()
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Batch>().HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Match>().HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Match>()
            .HasOne<CardTransaction>()
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Session>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        foreach (var entity in b.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
            {
                property.SetPrecision(20);
                property.SetScale(6);
            }
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
            if (
                (
                    entry.Entity
                    is PostingLineRecord
                        or PostingReview
                        or Audit
                        or Snapshot
                        or Decision
                        or Review
                        or LineComment
                        or SupplierAccount
                        or ExtractionAttempt
                        or DocumentEvent
                        or FinancePolicyVersion
                        or ExpenseFxProposal
                        or ExpenseFxDecision
                        or ExpenseCalculation
                        or TaxPolicyDraft
                        or TaxPolicyDecision
                        or VerifiedVatComponent
                        or ReportReset
                        or ReportRestore
                        or BankTreatment
                        or BankTreatmentRevocation
                        or FinanceAcceptanceRecord
                        or CardTransaction
                ) && (entry.State is EntityState.Modified or EntityState.Deleted)
            )
                throw new InvalidOperationException("Immutable evidence");
        foreach (var entry in ChangeTracker.Entries())
            if (
                entry.Entity is IOriginalDocument
                && entry.State == EntityState.Modified
                && entry.Properties.Any(p =>
                    p.IsModified && p.Metadata.Name is not ("Scan" or "Ocr" or "Archived" or "CoveredLines")
                )
            )
                throw new InvalidOperationException("Immutable original metadata");
        foreach (var entry in ChangeTracker.Entries<DocumentReview>())
            if (
                entry.State == EntityState.Deleted
                || entry.State == EntityState.Modified
                    && entry.Properties.Any(p => p.IsModified && p.Metadata.Name != "Dispositions")
            )
                throw new InvalidOperationException("Immutable extraction source");
        foreach (var entry in ChangeTracker.Entries<Batch>())
            if (
                entry.State == EntityState.Deleted
                || entry.State == EntityState.Modified
                    && entry.Properties.Any(p =>
                        p.IsModified && p.Metadata.Name is not ("State" or "Reference" or "PostedAt")
                    )
            )
                throw new InvalidOperationException("Immutable export batch");
        return base.SaveChangesAsync(ct);
    }
}
