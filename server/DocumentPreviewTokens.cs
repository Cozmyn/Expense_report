using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Decont;

public record PreviewGrant(Guid Report, Guid Document, string Actor, DateTime Expires);

public static class DocumentPreviewTokens
{
    static IDataProtector Protector(HttpContext c) =>
        c.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("decont.document-preview.v1");

    public static string Issue(HttpContext c, Guid report, Guid document) =>
        Protector(c)
            .Protect(
                Serialization.Write(
                    new PreviewGrant(report, document, Reports.Actor(c.User), DateTime.UtcNow.AddMinutes(5))
                )
            );

    public static void Validate(HttpContext c, Guid report, Guid document)
    {
        var token = c.Request.Query["viewer"].ToString();
        if (token == "")
            return;
        PreviewGrant grant;
        try
        {
            grant = Serialization.Read<PreviewGrant>(Protector(c).Unprotect(token));
        }
        catch (Exception e)
            when (e is CryptographicException or FormatException or ArgumentException or System.Text.Json.JsonException)
        {
            throw new DomainException(
                "preview_token",
                "Previzualizarea a expirat. Reîncearcă.",
                "Preview expired. Retry.",
                403
            );
        }
        Guard.Require(
            grant.Report == report
                && grant.Document == document
                && grant.Actor == Reports.Actor(c.User)
                && grant.Expires > DateTime.UtcNow,
            "preview_token",
            "Previzualizarea a expirat. Reîncearcă.",
            "Preview expired. Retry.",
            403
        );
    }
}
