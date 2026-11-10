using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Decont;

public static class SchemaUpgrades
{
    public static async Task Apply(Db db)
    {
        var ddl = db.Database.GenerateCreateScript();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (
            System.Text.RegularExpressions.Match table in Regex.Matches(
                ddl,
                @"CREATE TABLE ""(?:ReportAttachments|Preferences|Comments|Assignments|CostCenterOwners|SchemaReleases|SupplierAccounts|DocumentReviews|DocumentGroups|ExtractionAttempts|DocumentEvents|FinancePolicies|FxProposals|FxDecisions|Calculations|TaxPolicies|TaxPolicyDecisions|VerifiedVatComponents|ReportResets|ReportRestores|BankTreatments|BankTreatmentRevocations|FinanceAcceptanceRecords|PostingLines|PostingReviews)"" \(.*?\n\);",
                RegexOptions.Singleline
            )
        )
            await db.Database.ExecuteSqlRawAsync(table.Value.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS "));
        foreach (
            var column in new[]
            {
                ("Batches", "SnapshotJson", "TEXT NOT NULL DEFAULT '{{}}'"),
                ("Batches", "ReportVersion", "bigint NOT NULL DEFAULT 0"),
                ("Batches", "FinanceHash", "TEXT NOT NULL DEFAULT ''"),
                ("Batches", "BusinessKey", "TEXT NOT NULL DEFAULT ''"),
                ("Batches", "PostingFingerprint", "TEXT NOT NULL DEFAULT ''"),
                ("Batches", "Reconciliation", "TEXT NOT NULL DEFAULT '[]'"),
                ("Lines", "FinancialRounding", "INTEGER NULL"),
                ("Lines", "FinanceRon", db.Database.IsNpgsql() ? "numeric(20,6) NULL" : "TEXT NULL"),
                ("Lines", "FxDecisionId", db.Database.IsNpgsql() ? "uuid NULL" : "TEXT NULL"),
                ("Lines", "CalculationId", db.Database.IsNpgsql() ? "uuid NULL" : "TEXT NULL"),
                ("Lines", "CalculatedJson", "TEXT NULL"),
                ("Lines", "CalculationHash", "TEXT NULL"),
                (
                    "Lines",
                    "RoundingRon",
                    db.Database.IsNpgsql() ? "numeric(20,6) NOT NULL DEFAULT 0" : "TEXT NOT NULL DEFAULT '0'"
                ),
                ("Jobs", "StartedAt", db.Database.IsNpgsql() ? "timestamp with time zone NULL" : "TEXT NULL"),
                ("Jobs", "CompletedAt", db.Database.IsNpgsql() ? "timestamp with time zone NULL" : "TEXT NULL"),
                ("Jobs", "FailureCode", "TEXT NULL"),
                ("ReportAttachments", "UploadedBy", "TEXT NOT NULL DEFAULT ''"),
                ("ReportAttachments", "Capture", "BOOLEAN NOT NULL DEFAULT FALSE"),
                ("ReportAttachments", "ReceiptMode", "TEXT NOT NULL DEFAULT 'Auto'"),
                ("Reports", "CostCenter", "TEXT NOT NULL DEFAULT ''"),
                ("AspNetUsers", "CostCenter", "TEXT NOT NULL DEFAULT ''"),
                ("Reports", "EmployeeName", "TEXT NOT NULL DEFAULT ''"),
                ("Reports", "EmployeeWorker", "TEXT NOT NULL DEFAULT ''"),
                ("Reports", "DepartmentCode", "TEXT NOT NULL DEFAULT ''"),
                ("Reports", "Payment", "TEXT NULL"),
                ("Reports", "CardId", db.Database.IsNpgsql() ? "uuid NULL" : "TEXT NULL"),
                ("Lines", "VatOverrideReason", "TEXT NULL"),
                ("Decisions", "RevisionHash", "TEXT NOT NULL DEFAULT ''"),
                ("Batches", "FileName", "TEXT NOT NULL DEFAULT ''"),
                ("Notices", "Hidden", "BOOLEAN NOT NULL DEFAULT FALSE"),
            }
        )
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = tx.GetDbTransaction();
            command.CommandText = db.Database.IsNpgsql()
                ? "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='"
                    + column.Item1
                    + "'"
                : "PRAGMA table_info(\"" + column.Item1 + "\")";
            var names = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    names.Add(reader.GetString(db.Database.IsNpgsql() ? 0 : 1));
            }
            if (!names.Contains(column.Item2))
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"" + column.Item1 + "\" ADD COLUMN \"" + column.Item2 + "\" " + column.Item3
                );
        }
        foreach (
            System.Text.RegularExpressions.Match index in Regex.Matches(
                ddl,
                @"CREATE (?:UNIQUE )?INDEX ""[^""]+"" ON ""(?:ReportAttachments|Preferences|Comments|Assignments|CostCenterOwners|SchemaReleases|SupplierAccounts|DocumentReviews|DocumentGroups|ExtractionAttempts|DocumentEvents|FinancePolicies|FxProposals|FxDecisions|Calculations|TaxPolicies|TaxPolicyDecisions|VerifiedVatComponents|ReportResets|ReportRestores|BankTreatments|BankTreatmentRevocations|FinanceAcceptanceRecords|PostingLines|PostingReviews)"".*?;"
            )
        )
            await db.Database.ExecuteSqlRawAsync(
                index
                    .Value.Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                    .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ")
            );
        foreach (
            var table in new[]
            {
                "Audits",
                "Snapshots",
                "Decisions",
                "Reviews",
                "Comments",
                "ExtractionAttempts",
                "DocumentEvents",
                "FinancePolicies",
                "FxProposals",
                "FxDecisions",
                "Calculations",
                "TaxPolicies",
                "TaxPolicyDecisions",
                "VerifiedVatComponents",
                "ReportResets",
                "ReportRestores",
                "BankTreatments",
                "BankTreatmentRevocations",
                "FinanceAcceptanceRecords",
                "PostingLines",
                "PostingReviews",
                "Transactions",
            }
        )
        {
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE OR REPLACE FUNCTION decont_immutable_evidence() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Immutable evidence'; END $$;"
                );
                await db.Database.ExecuteSqlRawAsync(
                    "DROP TRIGGER IF EXISTS immutable_evidence ON \""
                        + table
                        + "\"; CREATE TRIGGER immutable_evidence BEFORE UPDATE OR DELETE ON \""
                        + table
                        + "\" FOR EACH ROW EXECUTE FUNCTION decont_immutable_evidence();"
                );
            }
            else
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE TRIGGER IF NOT EXISTS immutable_update_"
                        + table
                        + " BEFORE UPDATE ON \""
                        + table
                        + "\" BEGIN SELECT RAISE(ABORT, 'Immutable evidence'); END;"
                );
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE TRIGGER IF NOT EXISTS immutable_delete_"
                        + table
                        + " BEFORE DELETE ON \""
                        + table
                        + "\" BEGIN SELECT RAISE(ABORT, 'Immutable evidence'); END;"
                );
            }
        }
        // Preserve export payloads even for direct SQL; lifecycle confirmation fields stay writable.
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE OR REPLACE FUNCTION decont_immutable_batch() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Immutable export batch'; END IF; IF (to_jsonb(NEW) - ARRAY['State','Reference','PostedAt']) IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['State','Reference','PostedAt']) THEN RAISE EXCEPTION 'Immutable export batch'; END IF; RETURN NEW; END $$;"
            );
            await db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER IF EXISTS immutable_batch ON \"Batches\"; CREATE TRIGGER immutable_batch BEFORE UPDATE OR DELETE ON \"Batches\" FOR EACH ROW EXECUTE FUNCTION decont_immutable_batch();"
            );
        }
        else
        {
            var immutable = new[]
            {
                "Id",
                "ReportId",
                "Revision",
                "Cycle",
                "RuleVersion",
                "Rows",
                "Key",
                "FileHash",
                "Hash",
                "Actor",
                "At",
                "FileName",
                "ReportVersion",
                "FinanceHash",
                "BusinessKey",
                "PostingFingerprint",
                "Reconciliation",
                "SnapshotJson",
            };
            var changes = string.Join(
                " OR ",
                immutable.Select(column => "OLD.\"" + column + "\" IS NOT NEW.\"" + column + "\"")
            );
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS immutable_batch_update");
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER immutable_batch_update BEFORE UPDATE ON \"Batches\" WHEN "
                    + changes
                    + " BEGIN SELECT RAISE(ABORT,'Immutable export batch'); END;"
            );
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER IF NOT EXISTS immutable_batch_delete BEFORE DELETE ON \"Batches\" BEGIN SELECT RAISE(ABORT,'Immutable export batch'); END;"
            );
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 3))
        {
            db.SchemaReleases.Add(new() { Id = 3, Name = "Receipt extraction and platform workflows" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 4))
        {
            db.SchemaReleases.Add(new() { Id = 4, Name = "Report header and employee context" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 5))
        {
            db.SchemaReleases.Add(new() { Id = 5, Name = "Versioned OCR document review and source groups" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 6))
        {
            db.SchemaReleases.Add(new() { Id = 6, Name = "Document outbox and processing diagnostics" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 7))
        {
            db.SchemaReleases.Add(new() { Id = 7, Name = "Finance FX policies and immutable calculation ledger" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 8))
        {
            db.SchemaReleases.Add(new() { Id = 8, Name = "Controlled tax policies and financial provenance" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 9))
        {
            db.SchemaReleases.Add(new() { Id = 9, Name = "Traceable workbook-compatible postings and Finance review" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 10))
        {
            db.SchemaReleases.Add(new() { Id = 10, Name = "Unified receipt intake and report-level cost centre" });
            await db.SaveChangesAsync();
        }
        if (!await db.SchemaReleases.AnyAsync(x => x.Id == 11))
        {
            db.SchemaReleases.Add(new() { Id = 11, Name = "Employee cost-centre defaults and manager ownership routing" });
            await db.SaveChangesAsync();
        }
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Batches_BusinessKey\" ON \"Batches\" (\"BusinessKey\") WHERE \"BusinessKey\" <> ''"
        );
        await tx.CommitAsync();
    }
}
