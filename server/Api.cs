using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class Api
{
    public static object View(Report r) =>
        new
        {
            r.Id,
            r.Number,
            r.Purpose,
            r.EmployeeId,
            r.EmployeeName,
            r.EmployeeWorker,
            r.DepartmentCode,
            r.Payment,
            r.CardId,
            r.ManagerId,
            r.State,
            r.Version,
            r.Revision,
            r.Created,
            r.Updated,
            r.RetainUntil,
            r.LegalHold,
            attachments = r.Attachments.Where(e => !e.Archived),
            total = r.Lines.Where(l => !l.Archived).Sum(l => l.Final()),
            personal = Rules.Personal(r),
            lines = r
                .Lines.Where(l => !l.Archived)
                .OrderBy(l => l.Position)
                .Select(l => new
                {
                    l.Id,
                    l.Position,
                    data = l.Data(),
                    l.EstimatedRon,
                    l.SettledRon,
                    l.Reconciled,
                    l.OverrideReason,
                    l.VatOverrideReason,
                    l.DuplicateDisposition,
                    l.FxVersion,
                    l.FinanceRon,
                    l.FxDecisionId,
                    l.CalculationId,
                    l.RoundingRon,
                    evidence = l.Evidence.Where(e => !e.Archived),
                }),
        };

    public static async Task<IResult> Atomic(
        Db db,
        HttpContext c,
        object request,
        Func<Task<object>> work,
        int successStatus = 200
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var key = c.Request.Headers["Idempotency-Key"].ToString();
        Guard.Require(
            key.Length is >= 16 and <= 100,
            "idempotency",
            "Identificatorul operațiunii lipsește.",
            "Operation identifier is missing.",
            400
        );
        var id = Serialization.Hash(Reports.Actor(c.User) + c.Request.Path + key);
        var hash = Serialization.Hash(Serialization.Write(request));
        var previous = await db.Idempotencies.FindAsync(id);
        if (previous != null)
        {
            Guard.Require(
                previous.RequestHash == hash,
                "idempotency_conflict",
                "Identificatorul a fost reutilizat.",
                "Operation identifier reused with different data.",
                409
            );
            return Results.Text(previous.Response, "application/json", statusCode: successStatus);
        }
        var result = await work();
        var response = Serialization.Write(result);
        db.Idempotencies.Add(
            new()
            {
                Id = id,
                Actor = Reports.Actor(c.User),
                RequestHash = hash,
                Response = response,
            }
        );
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Text(response, "application/json", statusCode: successStatus);
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
            "/reports/{id:guid}/expense-defaults",
            async (Guid id, string date, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                Guard.Require(DateOnly.TryParse(date, out var day), "date", "Data este invalidă.", "Invalid date.");
                return DocumentAssistance.Defaults(await db.Masters.ToListAsync(), report, day);
            }
        );
        DocumentStatus.Map(api);
        DocumentIntake.Map(api);
        DocumentEvents.Map(api);
        DocumentPages.Map(api);
        OcrReviews.Map(api);
        ReceiptFiles.Map(api);
        FinanceRouting.Map(api);
        DocumentPreviews.Map(api);
        DocumentCapture.Map(api);
        PlatformFeatures.Map(api);
        SpreadsheetImports.Map(api);
        Operations.Map(api);
        EmployeeImports.Map(api);
        SupplierValidation.Map(api);
        api.MapGet(
            "/reports",
            async (Reports svc, Db db, HttpContext c) =>
            {
                var rows = await svc.Scope(c.User).Include(r => r.Attachments).Include(r => r.Lines).ToListAsync();
                var users = await svc.Names();
                var actor = Reports.Actor(c.User);
                var day = DateOnly.FromDateTime(DateTime.UtcNow);
                var delegated = await db
                    .Delegations.Where(d => d.DelegateId == actor && d.From <= day && d.To >= day)
                    .Select(d => d.ManagerId)
                    .ToListAsync();
                return rows.OrderByDescending(r => r.Updated)
                    .Select(r =>
                    {
                        var view = Serialization.Read<Dictionary<string, object?>>(Serialization.Write(View(r)));
                        view["employeeName"] = users.GetValueOrDefault(r.EmployeeId);
                        view["managerName"] = users.GetValueOrDefault(r.ManagerId);
                        view["canApprove"] =
                            c.User.IsInRole("Manager")
                            && r.EmployeeId != actor
                            && (r.ManagerId == actor || delegated.Contains(r.ManagerId))
                            && (r.State is States.Manager or States.Reapproval);
                        view["ageDays"] = (int)(DateTime.UtcNow - r.Created).TotalDays;
                        return view;
                    });
            }
        );
        api.MapPost(
            "/reports",
            async (CreateReport i, Reports svc, Db db, HttpContext c) =>
                await Atomic(db, c, i, async () => View(await svc.Create(c.User, i.Purpose)))
        );
        api.MapGet(
            "/reports/{id:guid}",
            async (Guid id, Reports svc, Db db, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                var lineIds = r.Lines.Select(l => l.Id).ToList();
                return Results.Ok(
                    new
                    {
                        report = View(r),
                        sourceGroups = await db
                            .DocumentGroups.Where(g => g.ExpenseId != null && lineIds.Contains(g.ExpenseId.Value))
                            .Select(g => new
                            {
                                g.Id,
                                g.BundleId,
                                g.ExpenseId,
                                g.Regions,
                                g.Fields,
                                g.Taxes,
                                g.Warnings,
                            })
                            .ToListAsync(),
                        issues = await svc.Validate(r),
                        timeline = await db.Audits.Where(a => a.ReportId == id).OrderBy(a => a.At).ToListAsync(),
                        decisions = await db.Decisions.Where(d => d.ReportId == id).ToListAsync(),
                        comments = await db.Comments.Where(x => x.ReportId == id).OrderBy(x => x.At).ToListAsync(),
                        snapshots = await db
                            .Snapshots.Where(s => s.ReportId == id)
                            .Select(s => new
                            {
                                s.Revision,
                                s.Hash,
                                s.At,
                                s.Actor,
                            })
                            .ToListAsync(),
                    }
                );
            }
        );
        api.MapPatch(
            "/reports/{id:guid}",
            async (Guid id, ReportEdit i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        var before = new
                        {
                            r.Purpose,
                            r.Payment,
                            r.CardId,
                        };
                        if (i.Payment != null)
                            await svc.ConfigureHeader(r, i.Payment, i.CardId);
                        r.Purpose = i.Purpose.Trim();
                        Reports.Touch(r);
                        svc.Log(
                            c.User,
                            id,
                            "Report",
                            "HeaderChanged",
                            before,
                            new
                            {
                                r.Purpose,
                                r.Payment,
                                r.CardId,
                            },
                            i.Reason
                        );
                        return View(r);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/lines",
            async (Guid id, LineEdit i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        Guard.Require(
                            i.SourceAttachmentId == null
                                || r.Attachments.Any(a => a.Id == i.SourceAttachmentId && !a.Archived),
                            "source",
                            "Document sursă indisponibil.",
                            "Source document unavailable.",
                            404
                        );
                        if (i.SourceGroupId != null)
                        {
                            var existing = await db.DocumentGroups.SingleOrDefaultAsync(g =>
                                g.Id == i.SourceGroupId && g.BundleId == i.SourceAttachmentId
                            );
                            Guard.Require(
                                existing != null,
                                "ocr_group",
                                "Grup sursă invalid.",
                                "Invalid source group."
                            );
                            if (existing!.ExpenseId != null)
                            {
                                Guard.Require(
                                    r.Lines.Any(l => !l.Archived && l.Id == existing.ExpenseId),
                                    "ocr_consumed",
                                    "Bonul sursă nu mai este activ.",
                                    "Source receipt is no longer active.",
                                    409
                                );
                                return View(r);
                            }
                        }
                        if (i.SourceAttachmentId != null && i.SourceGroupId == null)
                            Guard.Require(
                                !await db.DocumentReviews.AnyAsync(x => x.Id == i.SourceAttachmentId),
                                "ocr_group",
                                "Selectează și verifică un grup de documente.",
                                "Select and verify a document group."
                            );
                        PlatformFeatures.AccountingPermission(c.User, i.Data, i.Reason);
                        Guard.Require(
                            r.Lines.Count(l => !l.Archived) < 50,
                            "limit",
                            "Limita este de 50 de cheltuieli.",
                            "Maximum 50 expenses."
                        );
                        var l = new Line { ReportId = id, Position = r.Lines.Count + 1 };
                        await svc.Apply(l, i.Data);
                        r.Lines.Add(l);
                        db.Lines.Add(l);
                        if (i.SourceGroupId != null)
                        {
                            Guard.Require(
                                i.SourceAttachmentId != null,
                                "source",
                                "Lipsește documentul sursă.",
                                "Source document is missing."
                            );
                            await OcrReviews.Verify(
                                db,
                                svc,
                                r,
                                l,
                                i.SourceAttachmentId!.Value,
                                i.SourceGroupId.Value,
                                c,
                                i.Reason
                            );
                        }
                        else
                            DocumentCapture.Link(r, l, i.SourceAttachmentId, svc, c, i.Reason);
                        Reports.Touch(r);
                        svc.Log(c.User, id, l.Id.ToString(), "LineAdded", after: i.Data, reason: i.Reason);
                        await svc.Reapproval(r, c.User);
                        return View(r);
                    }
                )
        );
        api.MapPatch(
            "/reports/{id:guid}/lines/{lineId:guid}",
            async (Guid id, Guid lineId, LineEdit i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        var l = r.Lines.Single(l => l.Id == lineId && !l.Archived);
                        PlatformFeatures.AccountingPermission(c.User, i.Data, i.Reason, l.Data().Accounting);
                        var before = l.Json;
                        await svc.Apply(l, i.Data);
                        if (i.SourceGroupId != null)
                        {
                            Guard.Require(
                                i.SourceAttachmentId != null
                                    && r.Attachments.Any(a => a.Id == i.SourceAttachmentId && !a.Archived),
                                "source",
                                "Document sursă indisponibil.",
                                "Source document unavailable.",
                                404
                            );
                            await OcrReviews.Verify(
                                db,
                                svc,
                                r,
                                l,
                                i.SourceAttachmentId!.Value,
                                i.SourceGroupId.Value,
                                c,
                                i.Reason
                            );
                        }
                        else
                        {
                            var group = await db.DocumentGroups.SingleOrDefaultAsync(g => g.ExpenseId == l.Id);
                            if (group != null)
                                await OcrReviews.Verify(db, svc, r, l, group.BundleId, group.Id, c, i.Reason);
                            else
                                DocumentCapture.Link(r, l, i.SourceAttachmentId, svc, c, i.Reason);
                        }
                        Reports.Touch(r);
                        svc.Log(c.User, id, lineId.ToString(), "LineChanged", before, i.Data, i.Reason);
                        await svc.Reapproval(r, c.User);
                        return View(r);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/lines/{lineId:guid}/archive",
            async (Guid id, Guid lineId, VersionRequest i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        var l = r.Lines.Single(l => l.Id == lineId && !l.Archived);
                        Guard.Require(
                            !await db.Matches.AnyAsync(m => m.LineId == lineId),
                            "matched",
                            "Anulează asocierea cardului.",
                            "Undo the card match first."
                        );
                        l.Archived = true;
                        var sourceGroup = await db.DocumentGroups.SingleOrDefaultAsync(g => g.ExpenseId == l.Id);
                        if (sourceGroup != null)
                        {
                            sourceGroup.State = "ReviewRequired";
                            sourceGroup.ExpenseId = null;
                            sourceGroup.VerifiedAt = null;
                            sourceGroup.VerifiedBy = null;
                            svc.Log(
                                c.User,
                                id,
                                sourceGroup.BundleId.ToString(),
                                "VerifiedSourceReopened",
                                after: new { sourceGroup.Id, lineId },
                                reason: i.Reason
                            );
                        }
                        Reports.Touch(r);
                        svc.Log(c.User, id, lineId.ToString(), "LineArchived", before: l.Json, reason: i.Reason);
                        return View(r);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/lines/{lineId:guid}/duplicate",
            async (Guid id, Guid lineId, VersionRequest i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        Guard.Require(
                            r.Lines.Count(l => !l.Archived) < 50,
                            "limit",
                            "Limita este de 50.",
                            "Maximum 50 expenses."
                        );
                        var source = r.Lines.Single(l => l.Id == lineId && !l.Archived);
                        var copy = new Line
                        {
                            ReportId = id,
                            Position = r.Lines.Count + 1,
                            Json = c.User.IsInRole("Finance")
                                ? source.Json
                                : Serialization.Write(source.Data() with { Accounting = null }),
                            EstimatedRon = source.EstimatedRon,
                            FxVersion = source.FxVersion,
                        };
                        r.Lines.Add(copy);
                        db.Lines.Add(copy);
                        Reports.Touch(r);
                        svc.Log(
                            c.User,
                            id,
                            copy.Id.ToString(),
                            "LineDuplicated",
                            after: new { source = lineId },
                            reason: i.Reason
                        );
                        await svc.Reapproval(r, c.User);
                        return View(r);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/submit",
            async (Guid id, VersionRequest i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        await svc.Submit(r, c.User);
                        return View(r);
                    }
                )
        );
        api.MapPost(
            "/reports/{id:guid}/actions/{action}",
            async (Guid id, string action, VersionRequest i, Reports svc, Db db, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        await svc.Action(r, c.User, action, i.Reason);
                        return View(r);
                    }
                )
        );
        api.MapGet(
            "/reports/{id:guid}/snapshots/{revision:int}",
            async (Guid id, int revision, Reports svc, Db db, HttpContext c) =>
            {
                await svc.Get(id, c.User);
                var s = await db.Snapshots.SingleAsync(s => s.ReportId == id && s.Revision == revision);
                return Results.Text(s.Json, "application/json");
            }
        );
        // Read-only financial preview: reuse the exact submit/preflight arithmetic, including RON tolerance.
        api.MapPost(
            "/vat-validation",
            async (ExpenseData d, Db db) =>
            {
                Guard.Require(d.Amount != 0, "amount", "Suma nu poate fi zero.", "Amount cannot be zero.");
                Guard.Require(DateOnly.TryParse(d.Date, out var day), "date", "Data nu este validă.", "Invalid date.");
                var masters = await db.Masters.ToListAsync();
                decimal estimated = Rules.Money(d.Amount);
                if (d.Currency != "RON")
                {
                    var fx = await FinancialCalculations.Estimate(db, d.Currency);
                    estimated = Rules.Money(d.Amount * fx.Rate);
                }
                var line = new Line { Json = Serialization.Write(d), EstimatedRon = estimated };
                var result = await FinancialCalculations.Preview(db, line);
                return new
                {
                    issues = result.Issues,
                    expected = d.Amount,
                    reconstructed = result.Vat?.ReconstructedGross,
                    calculation = result,
                };
            }
        );
        api.MapGet(
            "/rules",
            async (HttpContext c, Db db) =>
            {
                var q = c.Request.Query;
                var d = new ExpenseData(
                    "",
                    "",
                    "",
                    q["date"].ToString(),
                    q["country"].ToString(),
                    q["documentType"].ToString(),
                    q["category"].ToString(),
                    "",
                    q["payment"].ToString(),
                    null,
                    "",
                    null,
                    0,
                    q["currency"].ToString(),
                    [],
                    bool.TryParse(q["hasCompanyCui"], out var has) ? has : null
                );
                return Rules.FieldStates(d, await db.Masters.ToListAsync());
            }
        );
        api.MapGet(
            "/masters",
            async (Db db) =>
                await db
                    .Masters.Where(m =>
                        new[]
                        {
                            "costCenters",
                            "projects",
                            "expenseTypes",
                            "cards",
                            "vat",
                            "fx",
                            "fieldRules",
                            "countries",
                            "documentTypes",
                            "policies",
                        }.Contains(m.Kind)
                    )
                    .ToListAsync()
        );
        api.MapGet(
            "/notices",
            async (Db db, HttpContext c) =>
                await db
                    .Notices.Where(n => n.UserId == Reports.Actor(c.User) && !n.Hidden)
                    .OrderByDescending(n => n.At)
                    .Take(100)
                    .ToListAsync()
        );
        api.MapPost(
            "/notices/{id:guid}/read",
            async (Guid id, Db db, HttpContext c) =>
            {
                await db
                    .Notices.Where(n => n.Id == id && n.UserId == Reports.Actor(c.User))
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true));
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/reports/{id:guid}/lines/{lineId:guid}/evidence",
            async (Guid id, Guid lineId, HttpContext c, Db db, Reports svc, IStore store) =>
                await ReceiptFiles.Upload(id, lineId, c, db, svc, store)
        );
        api.MapGet(
            "/evidence/{id:guid}",
            async (Guid id, Db db, Reports svc, IStore store, HttpContext c) =>
            {
                var e = await db.Evidence.SingleAsync(e => e.Id == id);
                var l = await db.Lines.SingleAsync(l => l.Id == e.LineId);
                await svc.Get(l.ReportId, c.User);
                Guard.Require(
                    e.Scan == "Clean",
                    "quarantine",
                    "Documentul nu a trecut verificarea.",
                    "Document has not passed scanning."
                );
                var bytes = await store.Get(e.Key);
                Guard.Require(
                    Convert.ToHexString(SHA256.HashData(bytes)) == e.Hash,
                    "integrity",
                    "Integritatea documentului este invalidă.",
                    "Document integrity check failed."
                );
                return Results.File(bytes, e.Mime, enableRangeProcessing: true);
            }
        );
        api.MapPost(
            "/reports/{id:guid}/evidence/{eid:guid}/archive",
            async (Guid id, Guid eid, VersionRequest i, Db db, Reports svc, HttpContext c) =>
                await Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        var r = await svc.Version(id, i.Version, c.User);
                        svc.Edit(r, c.User, i.Reason);
                        var e = r.Lines.SelectMany(l => l.Evidence).Single(e => e.Id == eid);
                        e.Archived = true;
                        Reports.Touch(r);
                        svc.Log(c.User, id, eid.ToString(), "EvidenceArchived", reason: i.Reason);
                        return View(r);
                    }
                )
        );
    }
}

public record CreateReport(string Purpose);

public record ReportEdit(long Version, string Purpose, string? Reason, string? Payment = null, Guid? CardId = null);

public record LineEdit(
    long Version,
    ExpenseData Data,
    string? Reason,
    Guid? SourceAttachmentId = null,
    Guid? SourceGroupId = null
);

public record VersionRequest(long Version, string? Reason);
