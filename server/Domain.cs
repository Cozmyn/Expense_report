using System.Text.Json;
using Microsoft.AspNetCore.Identity;

namespace Decont;

public class User : IdentityUser
{
    public string Name { get; set; } = "";
    public string Worker { get; set; } = "";
    public string? ManagerId { get; set; }
    public bool Active { get; set; } = true;
    public bool ChangePassword { get; set; } = true;
}

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "ER-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    public string EmployeeId { get; set; } = "";
    public string ManagerId { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string EmployeeWorker { get; set; } = "";
    public string DepartmentCode { get; set; } = "";
    public string? Payment { get; set; }
    public Guid? CardId { get; set; }
    public string State { get; set; } = States.Draft;
    public long Version { get; set; } = 1;
    public int Revision { get; set; }
    public decimal ApprovedPersonal { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Updated { get; set; } = DateTime.UtcNow;
    public DateTime? RetainUntil { get; set; }
    public bool LegalHold { get; set; }
    public List<Line> Lines { get; set; } = [];
    public List<ReportAttachment> Attachments { get; set; } = [];
}

public class Line
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public int Position { get; set; }
    public bool Archived { get; set; }
    public string Json { get; set; } = "{}";
    public decimal EstimatedRon { get; set; }
    public decimal? SettledRon { get; set; }
    public bool Reconciled { get; set; }
    public string? VatOverrideReason { get; set; }
    public string? OverrideReason { get; set; }
    public string? DuplicateDisposition { get; set; }
    public string? FxVersion { get; set; }
    public List<Evidence> Evidence { get; set; } = [];

    public ExpenseData Data() => Serialization.Read<ExpenseData>(Json);

    public decimal? FinanceRon { get; set; }
    public Guid? FxDecisionId { get; set; }
    public Guid? CalculationId { get; set; }
    public string? CalculatedJson { get; set; }
    public string? CalculationHash { get; set; }
    public decimal RoundingRon { get; set; }
    public InvoiceRounding? FinancialRounding { get; set; }

    public decimal Final() => FinanceRon ?? EstimatedRon;
}

public record ExpenseData(
    string Supplier,
    string? Cui,
    string Invoice,
    string Date,
    string Country,
    string DocumentType,
    string Category,
    string Description,
    string Payment,
    Guid? CardId,
    string CostCenter,
    string? Project,
    decimal Amount,
    string Currency,
    List<TaxData> Taxes,
    bool? HasCompanyCui = null,
    bool SupplierConfirmed = false,
    string TaxCurrency = "RON",
    AccountingOverride? Accounting = null,
    string VatTreatment = "STANDARD",
    string? VatExplanation = null
);

public record TaxData(
    Guid RateId,
    decimal Base,
    decimal Vat,
    VatEvidenceState? State = null,
    string? EvidenceReference = null,
    int? EvidencePage = null
);

public interface IOriginalDocument
{
    Guid Id { get; }
    string Name { get; }
    string Key { get; }
    string Mime { get; }
    long Size { get; }
    string Hash { get; }
    string Scan { get; set; }
    bool Archived { get; }
    string? Ocr { get; set; }
}

public class ReportAttachment : IOriginalDocument
{
    public bool Capture { get; set; }
    public string UploadedBy { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public string CoveredLines { get; set; } = "[]";
    public string Name { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Key { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Size { get; set; }
    public string Hash { get; set; } = "";
    public string Scan { get; set; } = "Pending";
    public bool Archived { get; set; }
    public string? Ocr { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public bool Covers(Guid line) => Serialization.Read<List<Guid>>(CoveredLines).Contains(line);
}

public class Evidence : IOriginalDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LineId { get; set; }
    public string Name { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public string Key { get; set; } = "";
    public string Mime { get; set; } = "";
    public long Size { get; set; }
    public string Hash { get; set; } = "";
    public string Scan { get; set; } = "Pending";
    public bool Archived { get; set; }
    public string? Ocr { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
}

public class Snapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public int Revision { get; set; }
    public string Json { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Decision
{
    public string RevisionHash { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public int Revision { get; set; }
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Audit
{
    public long Id { get; set; }
    public Guid? ReportId { get; set; }
    public string Actor { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Reason { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Master
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public string Code { get; set; } = "";
    public string NameRo { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Json { get; set; } = "{}";
    public DateOnly From { get; set; }
    public DateOnly? To { get; set; }
    public bool Active { get; set; } = true;
}

public class VendorRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public string Cui { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string? Account { get; set; }
}

public class CardTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }
    public string Reference { get; set; } = "";
    public string Date { get; set; } = "";
    public string Merchant { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public decimal SettledRon { get; set; }
}

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Guid LineId { get; set; }
    public decimal Amount { get; set; }
    public decimal Ron { get; set; }
    public string Actor { get; set; } = "";
}

public class Batch
{
    public string SnapshotJson { get; set; } = "{}";
    public long ReportVersion { get; set; }
    public string FinanceHash { get; set; } = "";
    public string BusinessKey { get; set; } = "";
    public string PostingFingerprint { get; set; } = "";
    public string Reconciliation { get; set; } = "[]";
    public string FileName { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public int Revision { get; set; }
    public int Cycle { get; set; }
    public string RuleVersion { get; set; } = "";
    public string Rows { get; set; } = "";
    public string Key { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string Hash { get; set; } = "";
    public string State { get; set; } = "Exported";
    public string Actor { get; set; } = "";
    public string? Reference { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }
}

public class Notice
{
    public bool Hidden { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid ReportId { get; set; }
    public string Event { get; set; } = "";
    public bool Read { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Job
{
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "";
    public string Json { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime Due { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}

public class Delegation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ManagerId { get; set; } = "";
    public string DelegateId { get; set; } = "";
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}

public class Session
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public byte[] Ticket { get; set; } = [];
    public DateTime Expires { get; set; }
}

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public int Revision { get; set; }
    public string Actor { get; set; } = "";
    public string Checks { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Idempotency
{
    public string Id { get; set; } = "";
    public string Actor { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Response { get; set; } = "";
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public static class States
{
    public const string Draft = "DRAFT",
        Manager = "MANAGER_REVIEW",
        Rejected = "REJECTED",
        Returned = "EMPLOYEE_CORRECTION",
        Finance = "FINANCE_REVIEW",
        Reapproval = "REAPPROVAL_REQUIRED",
        Ready = "READY_FOR_ERP",
        Exported = "ERP_EXPORTED",
        Posted = "POSTED",
        Closed = "CLOSED";

    public static bool EmployeeEdit(string s) => s is Draft or Rejected or Returned;

    public static bool FinanceEdit(string s) => s is Finance or Ready or "PENDING_VENDOR" or "PENDING_CARD_MATCH";
}

public static class Serialization
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public static string Write(object x) => JsonSerializer.Serialize(x, Options);

    public static T Read<T>(string s) =>
        JsonSerializer.Deserialize<T>(s, Options) ?? throw new InvalidOperationException("Invalid persisted JSON");

    public static string Hash(string s) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s)));
}

public record Problem(string Code, string Ro, string En, Guid? LineId = null, bool Blocking = true);

public sealed class DomainException(string code, string ro, string en, int status = 422, Guid? lineId = null)
    : Exception(ro)
{
    public Guid? LineId => lineId;
    public string Code => code;
    public string En => en;
    public int Status => status;
}

public static class Guard
{
    public static void Require(bool ok, string code, string ro, string en, int status = 422)
    {
        if (!ok)
            throw new DomainException(code, ro, en, status);
    }

    public static void Reason(string? reason) =>
        Require(
            !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 3,
            "reason",
            "Completează un motiv.",
            "Provide a reason."
        );
}
