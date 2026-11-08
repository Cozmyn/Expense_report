using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class Documents
{
    public static void CheckExcelAmount(decimal amount) =>
        Guard.Require(
            Math.Abs(amount) <= long.MaxValue / 100m && (decimal)(double)amount == amount,
            "excel_precision",
            "Suma depășește precizia numerică Excel. Exportul a fost oprit pentru a păstra valoarea exactă.",
            "Amount exceeds Excel numeric precision. Export was stopped to preserve the exact value."
        );

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
                "/reports/{id:guid}/accounting-details",
                async (Guid id, Db db, Reports svc, HttpContext c) =>
                {
                    var r = await svc.Get(id, c.User);
                    var bytes = ReceiptAccountingExport.Generate(r, await db.Masters.ToListAsync());
                    svc.Log(c.User, id, "Report", "ReceiptAccountingDownloaded", after: new { r.Revision, r.Version });
                    await db.SaveChangesAsync();
                    return Results.File(
                        bytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        r.Number + "-bonuri-TVA.xlsx"
                    );
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance"));
        api.MapGet(
                "/reports/{id:guid}/erp-preview",
                async (Guid id, Reports svc, Db db, Posting posting, HttpContext c) =>
                    await PostingWorkflow.Preview(id, db, svc, posting, c)
            )
            .RequireAuthorization(p => p.RequireRole("Finance", "Auditor"));
        api.MapGet(
                "/batches/{id:guid}/xlsx",
                async (Guid id, Db db, Reports svc, IStore store, HttpContext c) =>
                {
                    var b =
                        await db.Batches.SingleOrDefaultAsync(b => b.Id == id)
                        ?? throw new DomainException("missing", "Export inexistent.", "Batch not found.", 404);
                    var report = await svc.Get(b.ReportId, c.User);
                    Guard.Require(
                        b.Hash == Serialization.Hash(b.Rows),
                        "integrity",
                        "Integritatea exportului este invalidă.",
                        "Batch integrity check failed."
                    );
                    var file = await store.Get(b.Key);
                    Guard.Require(
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)) == b.FileHash,
                        "integrity",
                        "Integritatea fișierului este invalidă.",
                        "File integrity check failed."
                    );
                    svc.Log(c.User, b.ReportId, b.Id.ToString(), "BatchDownloaded", after: b.Hash);
                    await db.SaveChangesAsync();
                    return Results.File(
                        file,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        string.IsNullOrEmpty(b.FileName)
                            ? Filename(b, report, await db.Masters.ToListAsync())
                            : b.FileName
                    );
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance"));
        api.MapGet(
            "/reports/{id:guid}/pdf",
            async (Guid id, Db db, Reports svc, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                var employee = await db.Users.SingleAsync(u => u.Id == r.EmployeeId);
                var bytes = ReportPdf.Generate(
                    r,
                    employee,
                    await db.Decisions.Where(d => d.ReportId == id).ToListAsync(),
                    await db.Masters.ToListAsync(),
                    await db.Reviews.Where(review => review.ReportId == id).ToListAsync(),
                    await db.Batches.Where(batch => batch.ReportId == id).ToListAsync(),
                    await svc.Names()
                );
                svc.Log(c.User, id, "Report", "PDFDownloaded");
                await db.SaveChangesAsync();
                return Results.File(bytes, "application/pdf", r.Number + ".pdf");
            }
        );
        api.MapGet(
            "/reports/{id:guid}/pack",
            async (Guid id, Db db, Reports svc, IStore store, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                using var output = new MemoryStream();
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    async Task Write(string name, byte[] bytes)
                    {
                        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                        await using var s = entry.Open();
                        await s.WriteAsync(bytes);
                    }
                    await Write("report.json", Encoding.UTF8.GetBytes(Serialization.Write(Api.View(r))));
                    await Write("report.html", Encoding.UTF8.GetBytes(Html(r, await db.Masters.ToListAsync())));
                    await Write(
                        "report.pdf",
                        ReportPdf.Generate(
                            r,
                            await db.Users.SingleAsync(u => u.Id == r.EmployeeId),
                            await db.Decisions.Where(d => d.ReportId == id).ToListAsync(),
                            await db.Masters.ToListAsync(),
                            await db.Reviews.Where(review => review.ReportId == id).ToListAsync(),
                            await db.Batches.Where(batch => batch.ReportId == id).ToListAsync(),
                            await svc.Names()
                        )
                    );
                    await Write(
                        "snapshots.json",
                        Encoding.UTF8.GetBytes(
                            Serialization.Write(await db.Snapshots.Where(s => s.ReportId == id).ToListAsync())
                        )
                    );
                    await Write(
                        "audit.json",
                        Encoding.UTF8.GetBytes(
                            Serialization.Write(await db.Audits.Where(a => a.ReportId == id).ToListAsync())
                        )
                    );
                    var docs = r
                        .Lines.SelectMany(l => l.Evidence)
                        .Cast<IOriginalDocument>()
                        .Concat(r.Attachments)
                        .Where(e => e.Scan == "Clean")
                        .ToList();
                    await Write(
                        "manifest.json",
                        Encoding.UTF8.GetBytes(
                            Serialization.Write(
                                docs.Select(e => new
                                {
                                    e.Id,
                                    e.Name,
                                    e.Hash,
                                    e.Size,
                                    e.Archived,
                                })
                            )
                        )
                    );
                    foreach (var e in docs)
                    {
                        var bytes = await store.Get(e.Key);
                        Guard.Require(
                            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == e.Hash,
                            "integrity",
                            "Integritatea documentului este invalidă.",
                            "Document integrity check failed."
                        );
                        await Write("originals/" + e.Id + Path.GetExtension(e.Name), bytes);
                    }
                }
                svc.Log(c.User, id, "Report", "PackDownloaded");
                await db.SaveChangesAsync();
                return Results.File(output.ToArray(), "application/zip", r.Number + ".zip");
            }
        );
        api.MapGet(
            "/reports/{id:guid}/print",
            async (Guid id, Reports svc, Db db, HttpContext c) =>
                Results.Content(
                    Html(await svc.Get(id, c.User), await db.Masters.ToListAsync()),
                    "text/html",
                    Encoding.UTF8
                )
        );
        api.MapGet(
            "/analytics",
            async (Reports svc, HttpContext c) =>
            {
                var rows = await svc.Scope(c.User).Include(r => r.Lines).ToListAsync();
                return new
                {
                    reports = rows.Count,
                    total = rows.Sum(r => r.Lines.Where(l => !l.Archived).Sum(l => l.Final())),
                    states = rows.GroupBy(r => r.State).Select(g => new { state = g.Key, count = g.Count() }),
                    categories = rows.SelectMany(r => r.Lines.Where(l => !l.Archived))
                        .GroupBy(l => l.Data().Category)
                        .Select(g => new { category = g.Key, total = g.Sum(l => l.Final()) }),
                };
            }
        );
        api.MapGet(
                "/analytics/csv",
                async (Reports svc, Db db, HttpContext c) =>
                {
                    var rows = await svc.Scope(c.User).Include(r => r.Lines).ToListAsync();
                    var csv =
                        "Report,Employee,State,Created,Category,CostCenter,Project,Payment,Currency,Amount,FinalRON\r\n"
                        + string.Join(
                            "\r\n",
                            rows.SelectMany(r =>
                                r.Lines.Where(l => !l.Archived)
                                    .Select(l =>
                                    {
                                        var d = l.Data();
                                        return string.Join(
                                            ",",
                                            new object?[]
                                            {
                                                r.Number,
                                                r.EmployeeId,
                                                r.State,
                                                r.Created.ToString("O"),
                                                d.Category,
                                                d.CostCenter,
                                                d.Project,
                                                d.Payment,
                                                d.Currency,
                                                d.Amount,
                                                l.Final(),
                                            }.Select(Csv.Cell)
                                        );
                                    })
                            )
                        );
                    svc.Log(c.User, null, "Analytics", "CSVDownloaded", after: new { reports = rows.Count });
                    await db.SaveChangesAsync();
                    return Results.File(Encoding.UTF8.GetBytes("\uFEFF" + csv), "text/csv", "decont-raport.csv");
                }
            )
            .RequireAuthorization(p => p.RequireRole("Finance", "Admin", "Auditor"));
    }

    public static string Filename(Batch batch, Report report, List<Master> masters)
    {
        var settings = masters.FirstOrDefault(m =>
            m.Kind == "erpSettings" && m.Code == "default" && Rules.Effective(m, DateOnly.FromDateTime(batch.At))
        );
        var pattern = settings == null ? "" : Rules.Text(settings, "filenamePattern");
        if (pattern == "")
            pattern = "Import-{number}-{cycle}-{date}";
        var filename = pattern
            .Replace("{number}", report.Number)
            .Replace("{cycle}", batch.Cycle.ToString())
            .Replace("{date}", batch.At.ToString("yyyyMMdd"));
        filename = new string(
            filename.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.').Take(150).ToArray()
        );
        return (filename == "" ? "Import-" + batch.Id : filename.Replace(".xlsx", "")) + ".xlsx";
    }

    public static byte[] Workbook(List<string[]> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Import");
        for (int j = 0; j < 24; j++)
            sheet.Cell(1, j + 1).Value = Posting.Headers[j];
        for (int j = 0; j < rows.Count; j++)
        for (int k = 0; k < 24; k++)
        {
            if (
                (k is 13 or 14)
                && decimal.TryParse(
                    rows[j][k],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var amount
                )
            )
            {
                CheckExcelAmount(amount);
                sheet.Cell(j + 2, k + 1).Value = amount;
            }
            else
                sheet.Cell(j + 2, k + 1).Value = rows[j][k];
        }
        sheet.Columns().AdjustToContents();
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }

    public static string Html(Report r, List<Master>? masters = null)
    {
        string E(object? s) =>
            System.Net.WebUtility.HtmlEncode(Convert.ToString(s, System.Globalization.CultureInfo.InvariantCulture))
            ?? "";
        return "<!doctype html><html lang='ro'><meta charset='utf-8'><title>"
            + E(r.Number)
            + "</title><style>body{font:14px system-ui;margin:40px;color:#132e2a}table{border-collapse:collapse;width:100%}td,th{padding:10px;border-bottom:1px solid #ddd;text-align:left}h1{color:#096655}@media print{button{display:none}tr{break-inside:avoid}}</style><button onclick='window.print()'>Tipărește / Save PDF</button><h1>Decont de cheltuieli / Expense report</h1><p>"
            + E(r.Number)
            + " · "
            + E(r.State)
            + " · Revizia / Revision "
            + r.Revision
            + "</p><h2>"
            + E(r.Purpose)
            + "</h2><table><thead><tr><th>Data / Date<th>Furnizor / Supplier<th>Document<th>Descriere / Description<th>RON</thead><tbody>"
            + string.Join(
                "",
                r.Lines.Where(l => !l.Archived)
                    .Select(l =>
                    {
                        var d = l.Data();
                        return "<tr><td>"
                            + E(d.Date)
                            + "<td>"
                            + E(d.Supplier)
                            + "<td>"
                            + E(d.Invoice)
                            + "<td>"
                            + E(d.Description)
                            + "<td>"
                            + E(l.Final());
                    })
            )
            + "</tbody></table><h2>Total: "
            + E(r.Lines.Where(l => !l.Archived).Sum(l => l.Final()))
            + " RON</h2><p>Rambursare personală / Personal reimbursement: "
            + E(Rules.Personal(r))
            + " RON</p>"
            + string.Join(
                "",
                r.Lines.Where(l => !l.Archived)
                    .OrderBy(l => l.Position)
                    .Select(l =>
                    {
                        var d = l.Data();
                        return "<section><h3>Bon / Receipt "
                            + l.Position
                            + "</h3><p>Tip / Category: "
                            + E(d.Category)
                            + " · Data / Date: "
                            + E(d.Date)
                            + " · Total: "
                            + E(d.Amount)
                            + " "
                            + E(d.Currency)
                            + "</p><p>Furnizor România / Romanian supplier: "
                            + (d.Country == "RO" ? "Da / Yes" : "Nu / No")
                            + " · CUI companie / Company VAT ID: "
                            + (Rules.DomesticVat(d) ? "Da / Yes" : "Nu / No")
                            + " · CUI furnizor / Supplier VAT ID: "
                            + E(d.Cui)
                            + "</p>"
                            + (
                                Rules.DomesticVat(d)
                                    ? "<table><thead><tr><th>Cotă / Rate<th>Bază / Base<th>TVA plătit / VAT paid<th>Monedă / Currency</thead><tbody>"
                                        + string.Join(
                                            "",
                                            d.Taxes.Select(t =>
                                            {
                                                var m = masters?.FirstOrDefault(x => x.Id == t.RateId);
                                                return "<tr><td>"
                                                    + E(
                                                        m == null
                                                            ? t.RateId.ToString()
                                                            : Rules.Number(m, "rate").ToString() + "%"
                                                    )
                                                    + "<td>"
                                                    + E(t.Base)
                                                    + "<td>"
                                                    + E(t.Vat)
                                                    + "<td>"
                                                    + E(d.TaxCurrency);
                                            })
                                        )
                                        + "</tbody></table>"
                                    : ""
                            )
                            + "<p>Documente / Files: "
                            + string.Join(", ", l.Evidence.Where(e => !e.Archived).Select(e => E(e.Name)))
                            + "</p></section>";
                    })
            )
            + "<h3>Fișiere comune / Combined files</h3><p>"
            + string.Join(", ", r.Attachments.Where(e => !e.Archived).Select(e => E(e.Name)))
            + "</p></html>";
    }
}
