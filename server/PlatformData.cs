namespace Decont;

public class Preference { public string Id { get; set; } = ""; public string UserId { get; set; } = ""; public string Key { get; set; } = ""; public string Json { get; set; } = "{}"; }
public class LineComment { public Guid Id { get; set; } = Guid.NewGuid(); public Guid ReportId { get; set; } public Guid? LineId { get; set; } public int Revision { get; set; } public string Actor { get; set; } = ""; public string Text { get; set; } = ""; public DateTime At { get; set; } = DateTime.UtcNow; }
public class Assignment { public Guid Id { get; set; } = Guid.NewGuid(); public string UserId { get; set; } = ""; public string ManagerId { get; set; } = ""; public string OrganizationCode { get; set; } = ""; public bool Active { get; set; } = true; public DateOnly From { get; set; } public DateOnly? To { get; set; } }
public class SchemaRelease { public int Id { get; set; } public string Name { get; set; } = ""; public DateTime Applied { get; set; } = DateTime.UtcNow; }
public record CommentInput(long Version, Guid? LineId, string Text);
public record AccountingOverride(string MainAccount, string LocalAccount, string SubAccount);

public class SupplierAccount { public Guid Id { get; set; } = Guid.NewGuid(); public string Cui { get; set; } = ""; public string Account { get; set; } = ""; public DateOnly From { get; set; } public DateOnly? To { get; set; } public string Actor { get; set; } = ""; public DateTime At { get; set; } = DateTime.UtcNow; }
