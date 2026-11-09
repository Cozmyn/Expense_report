using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public record PostingReviewInput(long Version, string Fingerprint, string Action, string Reason);

public static class PostingWorkflow
{
    public static string FinanceHash(Report r) =>
        Serialization.Hash(
            Serialization.Write(
                new
                {
                    r.Id,
                    r.Revision,
                    r.EmployeeId,
                    r.EmployeeWorker,
                    r.DepartmentCode,
                    r.Payment,
                    r.CardId,
                    lines = r
                        .Lines.Where(l => !l.Archived)
                        .OrderBy(l => l.Position)
                        .Select(l => new
                        {
                            l.Id,
                            l.Position,
                            l.Json,
                            l.CalculationId,
                            l.CalculationHash,
                            l.CalculatedJson,
                            l.FinanceRon,
                            l.FxDecisionId,
                            l.RoundingRon,
                            l.EstimatedRon,
                        }),
                }
            )
        );

    public static PostingReview Accept(Db db, Report r, PostingResult result, ClaimsPrincipal actor, string reason)
    {
        var record = new PostingReview
        {
            ReportId = r.Id,
            Revision = r.Revision,
            Fingerprint = result.Fingerprint,
            Action = "ACCEPT",
            Actor = Reports.Actor(actor),
            Reason = reason,
        };
        db.PostingReviews.Add(record);
        return record;
    }

    public static async Task<object> Export(
        Guid id,
        VersionRequest request,
        Db db,
        Reports svc,
        Posting engine,
        IStore store,
        HttpContext c
    )
    {
        // Retry after a lost response may use a new HTTP key, but never creates a second batch.
        var report = await svc.Get(id, c.User);
        var existing = await db.Batches.SingleOrDefaultAsync(b =>
            b.ReportId == id && b.State != "Voided" && b.State != "Reversed"
        );
        if (existing != null)
        {
            Guard.Require(
                existing.ReportVersion > 0
                    && (request.Version == existing.ReportVersion || request.Version == report.Version)
                    && existing.FinanceHash == FinanceHash(report),
                "export_conflict",
                "Există un export pentru alte date/versiuni. Deschide istoricul exporturilor.",
                "An export already exists for different inputs/versions. Open export history.",
                409
            );
            svc.Log(c.User, id, existing.Id.ToString(), "ERPExportReused", after: existing.Hash);
            return new { batchId = existing.Id, report = Api.View(report) };
        }
        Guard.Require(
            report.Version == request.Version,
            "conflict",
            "Datele s-au modificat. Reîncarcă decontul.",
            "Data changed. Reload the report.",
            409
        );
        Guard.Require(
            report.State == States.Ready,
            "state",
            "Finalizează verificarea Finance.",
            "Complete the Finance review first."
        );
        Guard.Require(
            await db.Decisions.AnyAsync(d => d.ReportId == id && d.Revision == report.Revision && d.Action == "approve")
                && await db.Reviews.AnyAsync(x => x.ReportId == id && x.Revision == report.Revision),
            "review",
            "Lipsește aprobarea sau verificarea Finance pentru revizia curentă.",
            "Current revision approval or Finance review is missing."
        );
        var issues = await svc.Validate(report, true);
        issues.AddRange(await FinancialCalculations.ExportProblems(db, report));
        var masters = await db.Masters.ToListAsync();
        var production = c.RequestServices.GetRequiredService<IWebHostEnvironment>().IsProduction();
        issues.AddRange(FinanceAcceptance.MasterProblems(masters, production));
        issues.AddRange(await FinanceAcceptance.ReleaseProblems(db, production));
        Guard.Require(
            !issues.Any(x => x.Blocking),
            "preflight",
            Serialization.Write(issues),
            Serialization.Write(issues)
        );
        var result = engine.Build(
            report,
            (await db.Users.FindAsync(report.EmployeeId))!,
            masters,
            await db.SupplierAccounts.ToListAsync()
        );
        var review = await db
            .PostingReviews.Where(x => x.ReportId == id && x.Revision == report.Revision)
            .OrderByDescending(x => x.At)
            .FirstOrDefaultAsync();
        Guard.Require(
            review?.Action == "ACCEPT" && review.Fingerprint == result.Fingerprint,
            "posting_review",
            "Confirmă din nou previzualizarea contabilă; datele sau mapările s-au modificat.",
            "Accept the posting preview again; data or mappings changed."
        );
        var cycle = await db.Batches.CountAsync(b => b.ReportId == id) + 1;
        var financeHash = FinanceHash(report);
        var rowJson = Serialization.Write(result.Rows);
        var batch = new Batch
        {
            ReportId = id,
            Revision = report.Revision,
            ReportVersion = report.Version,
            Cycle = cycle,
            FinanceHash = financeHash,
            PostingFingerprint = result.Fingerprint,
            RuleVersion = result.Version,
            Rows = rowJson,
            Hash = Serialization.Hash(rowJson),
            Actor = Reports.Actor(c.User),
            BusinessKey = Serialization.Hash(id + "/" + cycle + "/" + financeHash + "/" + result.Fingerprint),
            Reconciliation = Serialization.Write(result.Reconciliation),
            SnapshotJson = Serialization.Write(
                new
                {
                    report.Number,
                    report.EmployeeId,
                    report.EmployeeName,
                    report.EmployeeWorker,
                    report.DepartmentCode,
                    report.Payment,
                    report.CardId,
                }
            ),
        };
        batch.FileName = Documents.Filename(batch, report, masters);
        var bytes = Documents.Workbook(result.Rows);
        batch.Key = "batches/" + batch.Id + ".xlsx";
        batch.FileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        await store.Put(batch.Key, bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        db.Batches.Add(batch);
        var calculations = result.Lines.Where(x => x.CalculationId != null).Select(x => x.CalculationId).ToList();
        var components = await db
            .VerifiedVatComponents.Where(x => calculations.Contains(x.CalculationId))
            .ToListAsync();
        var seenExpenses = new HashSet<Guid>();
        foreach (var line in result.Lines)
        {
            var component = components.SingleOrDefault(x =>
                x.CalculationId == line.CalculationId && x.LineId == line.ExpenseId && x.RateBps == line.RateBps
            );
            db.PostingLines.Add(
                PostingLineRecord.From(batch.Id, line, component?.Id, seenExpenses.Add(line.ExpenseId))
            );
        }
        report.State = States.Exported;
        Reports.Touch(report);
        svc.Log(
            c.User,
            id,
            batch.Id.ToString(),
            "ERPExported",
            after: new
            {
                batch.Hash,
                batch.Cycle,
                batch.RuleVersion,
                batch.FinanceHash,
                batch.PostingFingerprint,
                lines = result.Lines.Count,
            }
        );
        return new { batchId = batch.Id, report = Api.View(report) };
    }

    public static async Task<object> Preview(Guid id, Db db, Reports svc, Posting engine, HttpContext c)
    {
        var r = await svc.Get(id, c.User);
        var active = await db.Batches.SingleOrDefaultAsync(b =>
            b.ReportId == id && b.State != "Voided" && b.State != "Reversed"
        );
        if (active != null)
        {
            var saved = await db.PostingLines.Where(l => l.BatchId == active.Id).OrderBy(l => l.Sequence).ToListAsync();
            return new
            {
                headers = Posting.Headers,
                issues = Array.Empty<Problem>(),
                rows = Serialization.Read<List<string[]>>(active.Rows),
                lines = saved.Select(l => Serialization.Read<PostingTrace>(l.Json)),
                reconciliation = Serialization.Read<List<PostingReconciliation>>(active.Reconciliation),
                version = active.RuleVersion,
                fingerprint = active.PostingFingerprint,
                frozen = true,
                batchId = active.Id,
            };
        }
        var issues = await svc.Validate(r, true);
        issues.AddRange(await FinancialCalculations.ExportProblems(db, r));
        PostingResult? result = null;
        if (!issues.Any(x => x.Blocking))
        {
            try
            {
                result = engine.Build(
                    r,
                    (await db.Users.FindAsync(r.EmployeeId))!,
                    await db.Masters.ToListAsync(),
                    await db.SupplierAccounts.ToListAsync()
                );
            }
            catch (DomainException e)
            {
                issues.Add(new(e.Code, e.Message, e.En, e.LineId));
            }
        }
        var reviews = await db
            .PostingReviews.Where(x => x.ReportId == id && x.Revision == r.Revision)
            .OrderByDescending(x => x.At)
            .Take(25)
            .ToListAsync();
        return new
        {
            headers = Posting.Headers,
            issues,
            rows = result?.Rows ?? [],
            lines = result?.Lines ?? [],
            reconciliation = result?.Reconciliation ?? [],
            version = result?.Version,
            fingerprint = result?.Fingerprint,
            reviews,
            frozen = false,
        };
    }

    public static void Map(RouteGroupBuilder api)
    {
        var finance = api.MapGroup("/finance").RequireAuthorization(p => p.RequireRole("Finance"));
        finance.MapPost(
            "/reports/{id:guid}/posting-review",
            async (Guid id, PostingReviewInput i, Db db, Reports svc, Posting engine, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        Guard.Reason(i.Reason);
                        Guard.Require(
                            i.Action is "ACCEPT" or "REJECT",
                            "posting_action",
                            "Alege confirmare sau respingere.",
                            "Choose accept or reject."
                        );
                        var r = await svc.Version(id, i.Version, c.User);
                        Guard.Require(
                            States.FinanceEdit(r.State),
                            "state",
                            "Previzualizarea nu poate fi decisă în această stare.",
                            "Posting preview cannot be reviewed in this state."
                        );
                        var issues = await FinancialCalculations.ExportProblems(db, r);
                        Guard.Require(
                            !issues.Any(x => x.Blocking),
                            "calculation_stale",
                            Serialization.Write(issues),
                            Serialization.Write(issues)
                        );
                        var result = engine.Build(
                            r,
                            (await db.Users.FindAsync(r.EmployeeId))!,
                            await db.Masters.ToListAsync(),
                            await db.SupplierAccounts.ToListAsync()
                        );
                        Guard.Require(
                            i.Fingerprint == result.Fingerprint,
                            "posting_stale",
                            "Previzualizarea s-a modificat. Reîncarcă înainte de confirmare.",
                            "Preview changed. Reload before reviewing.",
                            409
                        );
                        var record =
                            i.Action == "ACCEPT"
                                ? Accept(db, r, result, c.User, i.Reason)
                                : new PostingReview
                                {
                                    ReportId = id,
                                    Revision = r.Revision,
                                    Fingerprint = result.Fingerprint,
                                    Action = "REJECT",
                                    Actor = Reports.Actor(c.User),
                                    Reason = i.Reason,
                                };
                        if (i.Action == "REJECT")
                        {
                            db.PostingReviews.Add(record);
                            r.State = States.Finance;
                        }
                        Reports.Touch(r);
                        svc.Log(
                            c.User,
                            id,
                            record.Id.ToString(),
                            "PostingPreview" + i.Action,
                            after: new { record.Fingerprint, record.Action },
                            reason: i.Reason
                        );
                        return Api.View(r);
                    }
                )
        );
        finance.MapGet(
            "/batches/{id:guid}/lines",
            async (Guid id, Db db, Reports svc, HttpContext c) =>
            {
                var b =
                    await db.Batches.FindAsync(id)
                    ?? throw new DomainException("missing", "Export inexistent.", "Export not found.", 404);
                await svc.Get(b.ReportId, c.User);
                return new
                {
                    batch = new
                    {
                        b.Id,
                        b.ReportId,
                        b.Revision,
                        b.Cycle,
                        b.RuleVersion,
                        b.Hash,
                        b.FileHash,
                        b.State,
                        b.Reference,
                        b.At,
                        b.PostingFingerprint,
                        b.FinanceHash,
                        b.SnapshotJson,
                    },
                    reconciliation = Serialization.Read<List<PostingReconciliation>>(b.Reconciliation),
                    lines = (
                        await db.PostingLines.Where(l => l.BatchId == id).OrderBy(l => l.Sequence).ToListAsync()
                    ).Select(l => new
                    {
                        l.Id,
                        l.SourceVatComponentId,
                        trace = Serialization.Read<PostingTrace>(l.Json),
                    }),
                };
            }
        );
        finance.MapGet("/posting-dashboard", Dashboard);
    }

    static async Task<object> Dashboard(Db db, HttpContext c)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly Date(string name, DateOnly fallback) =>
            DateOnly.TryParse(c.Request.Query[name], out var value) ? value : fallback;
        var from = Date("from", today.AddDays(-90));
        var to = Date("to", today);
        Guard.Require(
            from <= to && to.DayNumber - from.DayNumber <= 366,
            "date_range",
            "Alege cel mult 366 de zile.",
            "Choose at most 366 days."
        );
        var first = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var until = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var state = c.Request.Query["state"].ToString();
        var query = db.Batches.Where(b => b.At >= first && b.At <= until);
        if (state != "")
            query = query.Where(b => b.State == state);
        var total = await query.CountAsync();
        var page = int.TryParse(c.Request.Query["page"], out var parsed) ? Math.Clamp(parsed, 1, 10000) : 1;
        var batches = await query.OrderByDescending(b => b.At).Skip((page - 1) * 25).Take(25).ToListAsync();
        var summaries = db.PostingLines.Where(l =>
            l.ExpenseSummary && query.Any(b => b.Id == l.BatchId && b.State != "Voided" && b.State != "Reversed")
        );
        var departments = await summaries
            .GroupBy(l => l.Department)
            .Select(g => new { key = g.Key, minor = g.Sum(l => l.SourceGrossMinor) })
            .ToListAsync();
        var vendors = await summaries
            .GroupBy(l => l.Vendor)
            .Select(g => new { key = g.Key, minor = g.Sum(l => l.SourceGrossMinor) })
            .ToListAsync();
        var categories = await summaries
            .GroupBy(l => l.Category)
            .Select(g => new { key = g.Key, minor = g.Sum(l => l.SourceGrossMinor) })
            .ToListAsync();
        var exceptions = await db
            .Reports.Where(r => r.State == States.Finance || r.State == States.Ready)
            .Select(r => new
            {
                r.Id,
                r.Number,
                r.State,
                r.Updated,
            })
            .Take(100)
            .ToListAsync();
        return new
        {
            from,
            to,
            total,
            page,
            pageSize = 25,
            unmatched = await query.CountAsync(b => b.State == "Exported"),
            legacyBatches = await query.CountAsync(b => b.PostingFingerprint == ""),
            batches = batches.Select(b => new
            {
                b.Id,
                b.ReportId,
                b.Revision,
                b.Cycle,
                b.State,
                b.Reference,
                b.At,
                b.Hash,
                b.FileHash,
                b.PostingFingerprint,
                b.SnapshotJson,
            }),
            byDepartment = departments.Select(g => new { g.key, total = g.minor / 100m }),
            byVendor = vendors.Select(g => new { g.key, total = g.minor / 100m }),
            byCategory = categories.Select(g => new { g.key, total = g.minor / 100m }),
            exceptions,
        };
    }
}
