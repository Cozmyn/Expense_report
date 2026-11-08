using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Decont;

public record PostingTrace(string Id, int Sequence, Guid ExpenseId, int Position, string Strategy, Guid? RateId, int? RateBps, Guid? CalculationId, Guid? FxDecisionId, Guid MappingId, string MappingJson, decimal? VehiclePercentage, string Reason, decimal Debit, decimal Credit, string SourceJson, string Department, decimal SourceGrossRon, string[] Cells);
public record PostingReconciliation(Guid ExpenseId, int Position, decimal SourceGrossRon, decimal BaseRon, decimal VatRon, decimal RoundingRon, decimal ExportGrossRon, decimal DifferenceRon);
public record PostingResult(string Version, List<string[]> Rows, List<PostingTrace> Lines, List<PostingReconciliation> Reconciliation)
{
    static readonly JsonSerializerOptions HashOptions = new(Serialization.Options);
    static PostingResult() { HashOptions.Converters.Add(new CanonicalPostingDecimal()); }
    public string Fingerprint => Serialization.Hash(JsonSerializer.Serialize(new { engine = Posting.EngineVersion, Version, Rows, Lines, Reconciliation }, HashOptions));
}
sealed class CanonicalPostingDecimal : JsonConverter<decimal> { public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDecimal(); public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture)); }
public class PostingLineRecord
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid BatchId { get; set; }
    public string StableId { get; set; } = ""; public int Sequence { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid? CalculationId { get; set; }
    public Guid? FxDecisionId { get; set; }
    public Guid? SourceVatComponentId { get; set; }
    public Guid MappingId { get; set; }
    public string Strategy { get; set; } = ""; public string Reason { get; set; } = ""; public string AccountType { get; set; } = ""; public string Account { get; set; } = ""; public long DebitMinor { get; set; }
    public long CreditMinor { get; set; }
    public bool ExpenseSummary { get; set; }
    public long SourceGrossMinor { get; set; }
    public string Department { get; set; } = ""; public string Category { get; set; } = ""; public string Vendor { get; set; } = ""; public string Json { get; set; } = "{}";
    public static PostingLineRecord From(Guid batch, PostingTrace trace, Guid? component, bool firstExpenseLine) => new() { ExpenseSummary = firstExpenseLine, SourceGrossMinor = SignedMinor(trace.SourceGrossRon), Department = trace.Department, Category = Serialization.Read<ExpenseData>(trace.SourceJson).Category, Vendor = Serialization.Read<ExpenseData>(trace.SourceJson).Cui ?? Serialization.Read<ExpenseData>(trace.SourceJson).Supplier, BatchId = batch, StableId = trace.Id, Sequence = trace.Sequence, ExpenseId = trace.ExpenseId, CalculationId = trace.CalculationId, FxDecisionId = trace.FxDecisionId, SourceVatComponentId = component, MappingId = trace.MappingId, Strategy = trace.Strategy, Reason = trace.Reason, AccountType = trace.Cells[1], Account = trace.Cells[2], DebitMinor = Minor(trace.Debit), CreditMinor = Minor(trace.Credit), Json = Serialization.Write(trace) };
    public static long SignedMinor(decimal value) { Guard.Require(Math.Abs(value) <= long.MaxValue / 100m && decimal.Round(value, 2) == value, "posting_amount", "Totalul documentului nu poate fi reprezentat exact în bani.", "Document gross cannot be represented exactly in minor units."); return checked((long)(value * 100)); }
    public static long Minor(decimal value) { Guard.Require(value >= 0 && value <= long.MaxValue / 100m && decimal.Round(value, 2) == value, "posting_amount", "Valoarea ERP nu poate fi reprezentată exact în bani.", "ERP amount cannot be represented exactly in minor units."); return checked((long)(value * 100)); }
}
public class PostingReview { public Guid Id { get; set; } = Guid.NewGuid(); public Guid ReportId { get; set; } public int Revision { get; set; } public string Fingerprint { get; set; } = ""; public string Action { get; set; } = ""; public string Actor { get; set; } = ""; public string Reason { get; set; } = ""; public DateTime At { get; set; } = DateTime.UtcNow; }
