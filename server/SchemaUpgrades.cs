using System.Security.Cryptography;
using System.Text;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Decont;

public static class SchemaUpgrades
{
    private static readonly string[] ImmutableEvidenceTables =
    [
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
    ];

    // SHA-256 pins of the SQL reviewed for release 14. Old migrations are immutable:
    // any intentional DDL change requires a *new* numbered migration, not editing
    // these files. Do not use GenerateCreateScript to change production databases.
    private static readonly IReadOnlyDictionary<string, string> SqlHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["001-baseline.sqlite.sql"] = "A79ADBF1DD44568B1C378FAE7AF7EE62B2DE761401F629FB2DA48CC6746E6B22",
            ["013a-legacy-tables.sqlite.sql"] = "EF9E315B6F888295196A8DB3BAD6C444ECF3598F36FCD0FD0CD6F353476DFFDB",
            ["013b-legacy-indexes.sqlite.sql"] = "8E0F4C06504529193AEBF57F203E30640771306998D1CDC886541A91F3390CAF",
            ["001-baseline.postgres.sql"] = "227BE2B029BDF3FD162B1204F4C3AA2B8169227BBBFD2BE250D7EBE15B7CF251",
            ["013a-legacy-tables.postgres.sql"] = "B7D09F3DDB933911E6CB50F4529F459F2D95B0D2AD78C3BE62678A22184007E9",
            ["013b-legacy-indexes.postgres.sql"] = "8E0F4C06504529193AEBF57F203E30640771306998D1CDC886541A91F3390CAF",
        };

    private static string ReviewedSql(Db db, string step)
    {
        var name = step + (db.Database.IsNpgsql() ? ".postgres.sql" : ".sqlite.sql");
        if (!SqlHashes.TryGetValue(name, out var pinned))
            throw new InvalidOperationException("Unreviewed schema migration: " + name);
        using var stream = typeof(SchemaUpgrades).Assembly.GetManifestResourceStream("decont.migrations." + name)
            ?? throw new InvalidOperationException("Missing embedded schema migration: " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sql = reader.ReadToEnd();
        if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))) != pinned)
            throw new InvalidOperationException("Modified historical schema migration: " + name);
        return sql;
    }

    private static async Task<bool> TableExists(Db db, string name)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = db.Database.IsNpgsql()
            ? "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema=current_schema() AND table_name=@name)"
            : "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static async Task<long> TableCount(Db db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = db.Database.IsNpgsql()
            ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=current_schema() AND table_type='BASE TABLE'"
            : "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public static async Task VerifyRuntime(Db db)
    {
        if (!await HasVerifiedBaseline(db) || !await db.SchemaReleases.AsNoTracking().AnyAsync(x => x.Id == 14))
            throw new InvalidOperationException(
                "Database release 14 is required. Apply reviewed schema migrations with the isolated --migrate job.");
        if (await db.SchemaReleases.AsNoTracking().AnyAsync(x => x.Id > 14))
            throw new InvalidOperationException("Database schema is newer than this application image.");
        await VerifyModel(db);
    }

    /// <summary>
    /// Separate privileged migration job. No DDL is executed by normal startup.
    /// Lock, apply all reviewed statements transactionally, verify physical
    /// constraints, then stamp the release. No destructive down-migrations.
    /// </summary>
    public static async Task Apply(Db db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext(current_database()), 20261112)");
        var hasReleases = await TableExists(db, "SchemaReleases");
        if (hasReleases)
        {
            var version = await db.SchemaReleases.AsNoTracking().MaxAsync(r => (int?)r.Id) ?? 0;
            if (version > 14)
                throw new InvalidOperationException("Refusing to downgrade a newer database schema.");
            if (version >= 13)
            {
                // Existing verified baselines must never be silently repaired.
                await VerifyModel(db);
                if (version == 13)
                {
                    db.SchemaReleases.Add(new() { Id = 14, Name = "Explicit, pinned transactional migration baseline" });
                    await db.SaveChangesAsync();
                }
                await tx.CommitAsync();
                return;
            }
        }

        if (!await TableExists(db, "Reports"))
        {
            if (await TableCount(db) != 0)
                throw new InvalidOperationException("Refusing to adopt an unrelated or partially initialized database.");
            await db.Database.ExecuteSqlRawAsync(ReviewedSql(db, "001-baseline"));
        }
        else
        {
            // The only supported historical transition is an additive bridge to
            // the verified release-13 model. All table/index SQL is checked in
            // and hash pinned; only explicit enumerated columns are permitted.
            await db.Database.ExecuteSqlRawAsync(ReviewedSql(db, "013a-legacy-tables"));
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
                ("AspNetUsers", "ApprovalMode", "TEXT NOT NULL DEFAULT 'CostCenter'"),
                ("AspNetUsers", "FixedApproverId", "TEXT NULL"),
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

            await db.Database.ExecuteSqlRawAsync(ReviewedSql(db, "013b-legacy-indexes"));
        }

        await InstallImmutableGuards(db);
        // Never acknowledge a baseline on partially upgraded financial data.
        await VerifyModel(db);
        var releases = new (int Id, string Name)[]
        {
            (3, "Receipt extraction and platform workflows"),
            (4, "Report header and employee context"),
            (5, "Versioned OCR document review and source groups"),
            (6, "Document outbox and processing diagnostics"),
            (7, "Finance FX policies and immutable calculation ledger"),
            (8, "Controlled tax policies and financial provenance"),
            (9, "Traceable workbook-compatible postings and Finance review"),
            (10, "Unified receipt intake and report-level cost centre"),
            (11, "Employee cost-centre defaults and manager ownership routing"),
            (12, "Explicit fixed-approver exceptions for specified employees"),
            (13, "Verified schema baseline"),
            (14, "Explicit, pinned transactional migration baseline"),
        };
        var existing = (await db.SchemaReleases.AsNoTracking().Select(r => r.Id).ToListAsync()).ToHashSet();
        foreach (var (id, name) in releases)
            if (!existing.Contains(id))
                db.SchemaReleases.Add(new() { Id = id, Name = name });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static async Task InstallImmutableGuards(Db db)
    {
        foreach (var table in ImmutableEvidenceTables)
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

    }

    private static async Task<bool> HasVerifiedBaseline(Db db)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close)
            await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = db.Database.IsNpgsql()
                ? "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema=current_schema() AND table_name='SchemaReleases')"
                : "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='SchemaReleases')";
            if (!Convert.ToBoolean(await command.ExecuteScalarAsync()))
                return false;
        }
        finally
        {
            if (close)
                await db.Database.CloseConnectionAsync();
        }
        return await db.SchemaReleases.AsNoTracking().AnyAsync(r => r.Id == 13);
    }

    /// <summary>
    /// Read-only physical schema validation for both PostgreSQL and SQLite.
    /// Compare real columns/indexes with the EF model, including after upgrading
    /// a legacy database. A partially upgraded DB must not start as healthy.
    /// </summary>
    public static async Task VerifyModel(Db db)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close)
            await db.Database.OpenConnectionAsync();
        try
        {
            // EF 10 omits check-constraint metadata from the runtime-optimized
            // model; integrity verification uses the provider's design-time
            // model, which retains all DDL constraints.
            var tables = db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
                .Where(e => e.GetTableName() != null)
                .GroupBy(e => (Name: e.GetTableName()!, Schema: e.GetSchema()))
                .ToList();
            foreach (var table in tables)
            {
                var store = StoreObjectIdentifier.Table(table.Key.Name, table.Key.Schema);
                var expectedColumns = table.SelectMany(e => e.GetProperties())
                    .Select(p => p.GetColumnName(store))
                    .Where(name => name != null)
                    .ToHashSet(StringComparer.Ordinal);
                var expectedIndexes = table.SelectMany(e => e.GetIndexes())
                    .Select(index => index.GetDatabaseName())
                    .Where(name => name != null)
                    .ToHashSet(StringComparer.Ordinal);
                var expectedForeignKeyCount = table.SelectMany(e => e.GetForeignKeys()).Count();
                var expectedChecks = table.SelectMany(e => e.GetCheckConstraints())
                    .Select(check => check.Name).ToHashSet(StringComparer.Ordinal);

                async Task<HashSet<string>> ReadNames(string query, bool useNameParameter)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                    command.CommandText = query;
                    if (useNameParameter)
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@table";
                        parameter.Value = table.Key.Name;
                        command.Parameters.Add(parameter);
                    }
                    HashSet<string> names = new(StringComparer.Ordinal);
                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        names.Add(reader.GetString(0));
                    return names;
                }

                var columns = db.Database.IsNpgsql()
                    ? await ReadNames("SELECT column_name FROM information_schema.columns WHERE table_schema=current_schema() AND table_name=@table", true)
                    : await ReadNames("SELECT name FROM pragma_table_info('" + table.Key.Name.Replace("'", "''") + "')", false);
                var indexes = db.Database.IsNpgsql()
                    ? await ReadNames("SELECT indexname FROM pg_indexes WHERE schemaname=current_schema() AND tablename=@table", true)
                    : await ReadNames("SELECT name FROM sqlite_master WHERE type='index' AND tbl_name=@table", true);
                var requiredTriggers = ImmutableEvidenceTables.Contains(table.Key.Name)
                    ? db.Database.IsNpgsql()
                        ? new[] { "immutable_evidence" }
                        : new[] { "immutable_update_" + table.Key.Name, "immutable_delete_" + table.Key.Name }
                    : table.Key.Name == "Batches"
                        ? db.Database.IsNpgsql()
                            ? new[] { "immutable_batch" }
                            : new[] { "immutable_batch_update", "immutable_batch_delete" }
                        : [];
                var triggers = requiredTriggers.Length == 0
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : db.Database.IsNpgsql()
                        ? await ReadNames("SELECT t.tgname FROM pg_trigger t JOIN pg_class c ON t.tgrelid=c.oid JOIN pg_namespace n ON c.relnamespace=n.oid WHERE NOT t.tgisinternal AND n.nspname=current_schema() AND c.relname=@table", true)
                        : await ReadNames("SELECT name FROM sqlite_master WHERE type='trigger' AND tbl_name=@table", true);
                // Validate real FK/check constraints too, not merely EF indexes.
                // Otherwise a historical upgrade might pass while orphaning
                // receipts or permitting invalid immutable posting amounts.
                async Task<object?> Scalar(string query)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                    command.CommandText = query;
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@table";
                    parameter.Value = table.Key.Name;
                    command.Parameters.Add(parameter);
                    return await command.ExecuteScalarAsync();
                }
                var actualForeignKeyCount = Convert.ToInt32(await Scalar(
                    db.Database.IsNpgsql()
                        ? "SELECT COUNT(*) FROM pg_constraint f JOIN pg_class c ON c.oid=f.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=current_schema() AND c.relname=@table AND f.contype='f'"
                        : "SELECT COUNT(DISTINCT id) FROM pragma_foreign_key_list('" + table.Key.Name.Replace("'", "''") + "')"
                ));
                HashSet<string> actualChecks;
                if (db.Database.IsNpgsql())
                {
                    actualChecks = await ReadNames(
                        "SELECT f.conname FROM pg_constraint f JOIN pg_class c ON c.oid=f.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=current_schema() AND c.relname=@table AND f.contype='c'",
                        true);
                }
                else
                {
                    var createStatement = Convert.ToString(await Scalar(
                        "SELECT sql FROM sqlite_master WHERE type='table' AND name=@table")) ?? "";
                    actualChecks = expectedChecks
                        .Where(check => createStatement.Contains(check, StringComparison.Ordinal))
                        .ToHashSet(StringComparer.Ordinal);
                }
                var missingChecks = expectedChecks.Except(actualChecks).ToArray();
                var missingForeignKeys = Math.Max(0, expectedForeignKeyCount - actualForeignKeyCount);
                var missingTriggers = requiredTriggers.Except(triggers).ToArray();
                var missingColumns = expectedColumns.Except(columns).ToArray();
                var missingIndexes = expectedIndexes.Except(indexes).ToArray();
                if (columns.Count == 0 || missingColumns.Length > 0 || missingIndexes.Length > 0
                    || missingTriggers.Length > 0 || missingChecks.Length > 0 || missingForeignKeys > 0)
                    throw new InvalidOperationException(
                        "Database schema drift for " + table.Key.Name
                        + "; missing columns: " + string.Join(",", missingColumns)
                        + "; missing indexes: " + string.Join(",", missingIndexes)
                        + "; missing audit triggers: " + string.Join(",", missingTriggers)
                        + "; missing check constraints: " + string.Join(",", missingChecks)
                        + "; missing foreign keys: " + missingForeignKeys
                        + ". Stop startup and restore or apply reviewed migrations."
                    );
            }
        }
        finally
        {
            if (close)
                await db.Database.CloseConnectionAsync();
        }
    }

}
