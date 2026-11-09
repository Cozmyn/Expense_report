using System.Security.Cryptography;

namespace Decont;

public static class DocumentPreviews
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
            "/reports/{id:guid}/documents/{document:guid}/preview",
            async (Guid id, Guid document, Reports svc, IStore store, IConfiguration config, HttpContext c) =>
            {
                var r = await svc.Get(id, c.User);
                IOriginalDocument? file =
                    r.Attachments.FirstOrDefault(e => e.Id == document && !e.Archived)
                    ?? (IOriginalDocument?)
                        r.Lines.SelectMany(l => l.Evidence).FirstOrDefault(e => e.Id == document && !e.Archived);
                Guard.Require(file != null, "missing", "Document inexistent.", "Document not found.", 404);
                Guard.Require(
                    file!.Scan == "Clean",
                    "quarantine",
                    "Documentul nu a trecut scanarea.",
                    "Document has not passed scanning."
                );
                var bytes = await store.Get(file.Key, c.RequestAborted);
                Guard.Require(
                    Convert.ToHexString(SHA256.HashData(bytes)) == file.Hash,
                    "integrity",
                    "Integritatea documentului este invalidă.",
                    "Document integrity check failed."
                );
                if (file.Mime != "image/heic")
                    return Results.File(bytes, file.Mime, enableRangeProcessing: true);
                var temp = Path.Combine(Path.GetTempPath(), "decont-preview-" + Guid.NewGuid());
                Directory.CreateDirectory(temp);
                try
                {
                    var source = Path.Combine(temp, "original.heic");
                    var target = Path.Combine(temp, "preview.png");
                    await File.WriteAllBytesAsync(source, bytes, c.RequestAborted);
                    await LocalDocumentExtractor.Run(
                        config["Ocr:HeicConverter"] ?? "heif-convert",
                        [source, target],
                        c.RequestAborted
                    );
                    return Results.File(await File.ReadAllBytesAsync(target, c.RequestAborted), "image/png");
                }
                finally
                {
                    Directory.Delete(temp, true);
                }
            }
        );
    }
}
