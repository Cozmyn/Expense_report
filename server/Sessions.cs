using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Security.Cryptography;

namespace Decont;

public class Tickets(IServiceScopeFactory scopes, IDataProtectionProvider provider, IConfiguration config)
    : ITicketStore
{
    readonly IDataProtector protect = provider.CreateProtector("decont.server-session.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var id = Guid.NewGuid().ToString("N");
        await Save(id, ticket);
        return id;
    }

    public Task RenewAsync(string id, AuthenticationTicket ticket) => Save(id, ticket);

    async Task Save(string id, AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var row = await db.Sessions.FindAsync(id);
        if (row == null)
        {
            row = new() { Id = id };
            db.Sessions.Add(row);
        }
        row.UserId = ticket.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        row.Ticket = protect.Protect(TicketSerializer.Default.Serialize(ticket));
        row.Expires = (ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(30)).UtcDateTime;
        await db.SaveChangesAsync();
    }

    // SQLite can return SQLITE_BUSY while a scan/OCR job commits its result.
    // A temporary database failure must NEVER be mistaken for a revoked or
    // expired session: that would sign a user out in the middle of their edits.
    private static bool TransientDatabaseError(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is SqliteException sqlite && sqlite.SqliteErrorCode is 5 or 6)
                return true;
            if (current is PostgresException pg && pg.SqlState is "40001" or "40P01")
                return true;
        }
        return false;
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string id)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                return await RetrieveOnce(id);
            }
            catch (Exception ex) when (attempt < 3 && TransientDatabaseError(ex))
            {
                await Task.Delay(40 * (attempt + 1));
            }
        }
        // The last database failure is reported as a server error, never 401.
        return await RetrieveOnce(id);
    }

    private async Task<AuthenticationTicket?> RetrieveOnce(string id)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        var row = await db.Sessions.FindAsync(id);
        if (row == null || row.Expires < DateTime.UtcNow)
            return null;
        AuthenticationTicket? ticket;
        try
        {
            ticket = TicketSerializer.Default.Deserialize(protect.Unprotect(row.Ticket));
        }
        catch (CryptographicException)
        {
            // A genuinely invalid, rotated or tampered ticket is unauthenticated.
            return null;
        }
        if (ticket == null)
            return null;
        var user = await db.Users.FindAsync(row.UserId);
        if (user == null || !await PlatformFeatures.Active(db, user, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            db.Sessions.Remove(row);
            await db.SaveChangesAsync();
            return null;
        }

        // Sliding idle expiration is preserved without UPDATE on every GET.
        // API pages fetch reports, masters and notices concurrently; writing
        // this row for every request caused SQLITE_BUSY and spurious 401s.
        // Renew only when within five minutes of the original idle lifetime,
        // or sooner when Session:Minutes is configured below five minutes.
        var minutes = Math.Max(1, config.GetValue<int?>("Session:Minutes") ?? 30);
        var renewThreshold = DateTime.UtcNow.AddMinutes(
            Math.Max(0.5, minutes - Math.Min(5, minutes / 2.0)));
        if (row.Expires <= renewThreshold)
        {
            row.Expires = DateTime.UtcNow.AddMinutes(minutes);
            await db.SaveChangesAsync();
        }
        ticket.Properties.ExpiresUtc = new DateTimeOffset(row.Expires, TimeSpan.Zero);
        return ticket;
    }

    public async Task RemoveAsync(string id)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        await db.Sessions.Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}
