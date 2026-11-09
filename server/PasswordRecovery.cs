using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public static class PasswordRecovery
{
    public static void Map(WebApplication app)
    {
        app.MapPost(
                "/api/auth/forgot-password",
                async (ForgotInput i, UserManager<User> users, Db db, IConfiguration config) =>
                {
                    var enabled =
                        !string.IsNullOrWhiteSpace(config["Smtp:Host"])
                        && !string.IsNullOrWhiteSpace(config["Application:BaseUrl"]);
                    var user = await users.FindByNameAsync(i.Username.Trim());
                    if (
                        enabled
                        && user != null
                        && await PlatformFeatures.Active(db, user, DateOnly.FromDateTime(DateTime.UtcNow))
                        && !string.IsNullOrEmpty(user.Email)
                    )
                    {
                        var token = WebEncoders.Base64UrlEncode(
                            Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user))
                        );
                        var link =
                            config["Application:BaseUrl"]!.TrimEnd('/')
                            + "/?resetUser="
                            + Uri.EscapeDataString(user.Id)
                            + "&resetToken="
                            + token;
                        db.Jobs.Add(
                            new()
                            {
                                Kind = "Email",
                                Json = Serialization.Write(
                                    new
                                    {
                                        to = user.Email,
                                        subject = "Resetare parolă / Password reset",
                                        text = "Folosește acest link pentru resetarea parolei. Expiră în 15 minute. / Use this link to reset your password. Expires in 15 minutes.\n"
                                            + link,
                                    }
                                ),
                            }
                        );
                        db.Audits.Add(
                            new()
                            {
                                Actor = user.Id,
                                Entity = "Authentication",
                                Action = "PasswordRecoveryRequested",
                            }
                        );
                        await db.SaveChangesAsync();
                    }
                    return Results.Ok(
                        new
                        {
                            enabled,
                            ro = enabled
                                ? "Dacă utilizatorul este eligibil, vei primi un email. Verifică și folderul spam."
                                : "Resetarea prin email nu este configurată. Contactează administratorul.",
                            en = enabled
                                ? "If the account is eligible, you will receive an email. Check spam too."
                                : "Email recovery is not configured. Contact your administrator.",
                        }
                    );
                }
            )
            .RequireRateLimiting("login");
        app.MapPost(
                "/api/auth/reset-password",
                async (RecoveryInput i, UserManager<User> users, Db db) =>
                {
                    var user = await users.FindByIdAsync(i.UserId);
                    Guard.Require(
                        user != null && await PlatformFeatures.Active(db, user, DateOnly.FromDateTime(DateTime.UtcNow)),
                        "reset",
                        "Link invalid sau expirat.",
                        "Invalid or expired reset link."
                    );
                    string token;
                    try
                    {
                        token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(i.Token));
                    }
                    catch
                    {
                        throw new DomainException(
                            "reset",
                            "Link invalid sau expirat.",
                            "Invalid or expired reset link."
                        );
                    }
                    var result = await users.ResetPasswordAsync(user!, token, i.Password);
                    Guard.Require(
                        result.Succeeded,
                        "reset",
                        "Link invalid/expirat sau parola nu respectă politica.",
                        "Invalid/expired link or password policy failed."
                    );
                    user!.ChangePassword = false;
                    await users.UpdateAsync(user);
                    await db.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync();
                    db.Audits.Add(
                        new()
                        {
                            Actor = user.Id,
                            Entity = "Authentication",
                            Action = "PasswordRecovered",
                        }
                    );
                    await db.SaveChangesAsync();
                    return Results.Ok(new { reset = true });
                }
            )
            .RequireRateLimiting("login");
    }
}

public record ForgotInput(string Username);

public record RecoveryInput(string UserId, string Token, string Password);
