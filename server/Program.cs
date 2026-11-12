using System.Security.Claims;
using System.Threading.RateLimiting;
using Amazon.S3;
using Decont;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.EntityFrameworkCore;

var isMigrationJob = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(
    args.Where(a => a is not ("--init" or "--bootstrap" or "--schema" or "--migrate")).ToArray()
);
// Only a precisely enumerated ingress may set forwarded scheme/client IP.
// Never trust client-supplied X-Forwarded-* headers from the public Internet.
if (!builder.Environment.IsDevelopment() && !isMigrationJob)
{
    var configured = builder.Configuration["ReverseProxy:KnownProxies"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    if (configured.Length == 0 || configured.Any(ip => !IPAddress.TryParse(ip, out _)))
        throw new InvalidOperationException(
            "Production requires ReverseProxy:KnownProxies as an explicit comma-separated list of proxy IP addresses."
        );
    var proxies = configured.Select(IPAddress.Parse).ToArray();
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        foreach (var proxy in proxies)
            options.KnownProxies.Add(proxy);
    });
}
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
var postgres = builder.Configuration["Database:Provider"] == "Postgres";
if (!postgres && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Production requires PostgreSQL");
if (!builder.Environment.IsDevelopment())
{
    // Security gates belong in the application too, not only an optional
    // external deployment preflight. A DDL migrator must also verify server TLS.
    var connection = builder.Configuration.GetConnectionString("Database");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Production requires a PostgreSQL TLS connection.");
    var settings = new Npgsql.NpgsqlConnectionStringBuilder(connection);
    if (settings.SslMode != Npgsql.SslMode.VerifyFull || settings.TrustServerCertificate)
        throw new InvalidOperationException("Production PostgreSQL requires SSL Mode=VerifyFull without certificate bypass.");
    if (!isMigrationJob)
    {
        var s3Endpoint = builder.Configuration["Storage:Endpoint"];
        if (!Uri.TryCreate(s3Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo))
            throw new InvalidOperationException("Production S3 endpoint requires verified HTTPS.");
        var baseUrl = builder.Configuration["Application:BaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var publicUrl)
            || publicUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(publicUrl.UserInfo)
            || publicUrl.AbsolutePath != "/" || !string.IsNullOrEmpty(publicUrl.Query)
            || !string.IsNullOrEmpty(publicUrl.Fragment))
            throw new InvalidOperationException("Production requires an HTTPS application origin.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["Scanner:Host"])
            || builder.Configuration.GetValue<bool>("Ocr:Enabled") != true
            || string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"])
            || string.IsNullOrWhiteSpace(builder.Configuration["Smtp:From"]))
            throw new InvalidOperationException("Production requires configured scanner, OCR and SMTP.");
        var keysRoot = builder.Configuration["Data:Directory"];
        if (string.IsNullOrWhiteSpace(keysRoot) || !Path.IsPathFullyQualified(keysRoot))
            throw new InvalidOperationException("Production requires a persistent absolute Data Protection directory.");
    }
}
var dataRoot = builder.Configuration["Data:Directory"] ?? Path.Combine(builder.Environment.ContentRootPath, ".data");
Directory.CreateDirectory(dataRoot);
builder.Services.AddDbContext<Db>(o =>
{
    if (postgres)
        o.UseNpgsql(builder.Configuration.GetConnectionString("Database"));
    else
        o.UseSqlite("Data Source=" + Path.Combine(dataRoot, "decont.db"));
});
builder
    .Services.AddIdentity<User, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 12;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<Db>()
    .AddDefaultTokenProviders();
builder.Services.AddSingleton<Tickets>();
builder
    .Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<Tickets>(
        (o, s) =>
        {
            o.SessionStore = s;
            o.Cookie.Name = "decont.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue<int?>("Session:Minutes") ?? 30);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = c =>
            {
                c.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = c =>
            {
                c.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
        }
    );
builder
    .Services.AddDataProtection()
    .SetApplicationName("decont")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataRoot, "keys")));
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<Reports>();
builder.Services.AddSingleton<Posting>();
builder.Services.AddSingleton<IDocumentExtractor, LocalDocumentExtractor>();
builder.Services.AddSingleton<IDocumentScanner, ClamScanner>();
builder.Services.AddSingleton<ISupplierValidator, SupplierValidator>();
if (builder.Configuration["Storage:Provider"] == "S3")
{
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        builder.Configuration["Storage:AccessKey"],
        builder.Configuration["Storage:SecretKey"],
        new AmazonS3Config { ServiceURL = builder.Configuration["Storage:Endpoint"], ForcePathStyle = true }
    ));
    builder.Services.AddScoped<IStore, S3Store>();
}
else
{
    if (!builder.Environment.IsDevelopment() && !isMigrationJob)
        throw new InvalidOperationException("Production requires configured private S3 storage");
    builder.Services.AddScoped<IStore, LocalStore>();
}
if (!builder.Configuration.GetValue<bool>("Worker:Disabled"))
{
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddHostedService<ReminderWorker>();
}
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 100L * 1024 * 1024 + 1024 * 1024
);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 101L * 1024 * 1024);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
        "document-intake",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.User.Identity?.IsAuthenticated == true
                    ? Reports.Actor(c.User)
                    : c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 180,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
    o.AddPolicy(
        "document-preview",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.User.Identity?.IsAuthenticated == true
                    ? Reports.Actor(c.User)
                    : c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
    o.AddPolicy(
        "login",
        c =>
            RateLimitPartition.GetFixedWindowLimiter(
                c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromMinutes(15));
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
);
builder.Services.AddHttpContextAccessor();
builder
    .Services.AddOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>()
    .Configure<IHttpContextAccessor>(
        (options, context) => options.SerializerOptions.Converters.Add(new ExactMoneyJson(context))
    );
var app = builder.Build();
PasswordRecovery.Map(app);
if (args.Contains("--init") || args.Contains("--bootstrap") || args.Contains("--schema") || isMigrationJob)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    if (args.Contains("--schema"))
    {
        // Audit-only model DDL output. This is never applied to an existing DB.
        Console.Write(db.Database.GenerateCreateScript());
        return;
    }
    if (isMigrationJob)
    {
        await SchemaUpgrades.Apply(db);
        Console.WriteLine("Reviewed schema migrations applied and verified.");
        return;
    }
    if (!app.Environment.IsDevelopment() && args.Contains("--init"))
        throw new InvalidOperationException("Production schema changes require an isolated --migrate job with a dedicated DDL role.");
    if (app.Environment.IsDevelopment())
        await SchemaUpgrades.Apply(db);
    else
        await SchemaUpgrades.VerifyRuntime(db);
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Employee", "Manager", "Finance", "Admin", "Auditor", "FinanceReopen" })
        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new(role));
    if (args.Contains("--bootstrap"))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var name =
            builder.Configuration["Bootstrap:Username"]
            ?? throw new InvalidOperationException("Bootstrap username required");
        var existing = await users.FindByNameAsync(name);
        if (existing != null)
        {
            Guard.Require(
                existing.Active && await users.IsInRoleAsync(existing, "Admin"),
                "bootstrap",
                "Contul existent nu este un administrator activ.",
                "Existing bootstrap account is not an active administrator."
            );
            Console.WriteLine("Administrator already initialized; password unchanged.");
            return;
        }
        var user = new User
        {
            UserName = name,
            Name = "Administrator",
            Email = builder.Configuration["Bootstrap:Email"],
        };
        var result = await users.CreateAsync(
            user,
            builder.Configuration["Bootstrap:Password"]
                ?? throw new InvalidOperationException("Bootstrap password required")
        );
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(";", result.Errors.Select(e => e.Code)));
        await users.AddToRoleAsync(user, "Admin");
    }
    return;
}
// SQLite is Development/test-only. The pinned SQL baseline must not be
// edited for a connection-level tuning change; enable WAL on the runtime
// connection after the one-shot bootstrap transaction has committed. WAL
// permits OCR/scan readers alongside a writer and prevents session-read
// starvation that the old DELETE journaling mode allowed.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<Db>();
    // Integration tests can replace the provider after configuration; always
    // inspect the actual EF connection, not only Database:Provider.
    if (database.Database.IsSqlite())
        await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}
// Production application identity must have SELECT/DML only. Startup validates
// the pinned release and physical schema but never attempts DDL repairs.
if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await SchemaUpgrades.VerifyRuntime(scope.ServiceProvider.GetRequiredService<Db>());
}
app.Use(
    async (c, next) =>
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        c.Response.Headers["X-Request-ID"] = c.TraceIdentifier;
        try
        {
            await next();
        }
        catch (DomainException ex)
        {
            c.Response.StatusCode = ex.Status;
            await c.Response.WriteAsJsonAsync(
                new
                {
                    code = ex.Code,
                    ro = ex.Message,
                    en = ex.En,
                    lineId = ex.LineId,
                }
            );
        }
        catch (BadHttpRequestException)
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsJsonAsync(
                new
                {
                    code = "input",
                    ro = "Datele trimise nu sunt valide.",
                    en = "Invalid request data.",
                }
            );
        }
        catch (DbUpdateConcurrencyException)
        {
            c.Response.StatusCode = 409;
            await c.Response.WriteAsJsonAsync(
                new
                {
                    code = "conflict",
                    ro = "Datele au fost modificate. Reîncarcă.",
                    en = "Data changed. Reload.",
                }
            );
        }
        catch (AntiforgeryValidationException)
        {
            c.Response.StatusCode = 400;
            await c.Response.WriteAsJsonAsync(
                new
                {
                    code = "csrf",
                    ro = "Reîncarcă formularul.",
                    en = "Reload the form.",
                }
            );
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Request {Trace} failed", c.TraceIdentifier);
            c.Response.StatusCode = 500;
            await c.Response.WriteAsJsonAsync(
                new
                {
                    code = "server",
                    ro = "Operațiunea a eșuat.",
                    en = "Operation failed.",
                    trace = c.TraceIdentifier,
                }
            );
        }
        finally
        {
            Operations.Record(c.Response.StatusCode, watch.Elapsed.TotalMilliseconds);
        }
    }
);
app.Use(
    async (c, next) =>
    {
        c.Response.Headers["X-Content-Type-Options"] = "nosniff";
        c.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        c.Response.Headers["Referrer-Policy"] = "same-origin";
        if (c.Request.Path.StartsWithSegments("/api"))
            c.Response.Headers.CacheControl = "no-store";
        await next();
    }
);
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseRateLimiter();
app.Use(
    async (c, next) =>
    {
        if (c.Request.Path.StartsWithSegments("/api") && c.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
        if (c.User.Identity?.IsAuthenticated == true)
        {
            var db = c.RequestServices.GetRequiredService<Db>();
            var user = await db.Users.FindAsync(Reports.Actor(c.User));
            if (user == null || !await PlatformFeatures.Active(db, user, DateOnly.FromDateTime(DateTime.UtcNow)))
            {
                c.Response.StatusCode = 401;
                return;
            }
            var roles = await db
                .UserRoles.Where(x => x.UserId == user.Id)
                .Join(db.Roles, x => x.RoleId, y => y.Id, (x, y) => y.Name)
                .ToListAsync();
            if (c.User.Identity is ClaimsIdentity identity)
            {
                foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
                    identity.RemoveClaim(claim);
                foreach (var role in roles)
                    if (role != null)
                        identity.AddClaim(new(ClaimTypes.Role, role));
            }
            if (
                user.ChangePassword
                && c.Request.Path.StartsWithSegments("/api")
                && !new[] { "/api/me", "/api/auth/csrf", "/api/auth/logout", "/api/auth/change-password" }.Contains(
                    c.Request.Path.Value
                )
            )
            {
                c.Response.StatusCode = 428;
                await c.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "password_change",
                        ro = "Schimbă parola temporară.",
                        en = "Change the temporary password.",
                    }
                );
                return;
            }
        }
        await next();
    }
);
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { state = "alive" }));
app.MapGet(
    "/health/ready",
    async (Db db) => await db.Database.CanConnectAsync() ? Results.Ok(new { state = "ready" }) : Results.StatusCode(503)
);
app.MapGet(
    "/api/auth/csrf",
    (HttpContext c, IAntiforgery a) => Results.Ok(new { token = a.GetAndStoreTokens(c).RequestToken })
);
app.MapPost(
        "/api/auth/login",
        async (Login i, UserManager<User> users, SignInManager<User> sign, Db db) =>
        {
            var user = await users.FindByNameAsync(i.Username);
            var result =
                user != null && await PlatformFeatures.Active(db, user, DateOnly.FromDateTime(DateTime.UtcNow))
                    ? await sign.PasswordSignInAsync(user, i.Password, false, true)
                    : SignInResult.Failed;
            db.Audits.Add(
                new()
                {
                    Actor = user?.Id ?? "anonymous",
                    Entity = "Authentication",
                    Action = result.Succeeded ? "Login" : "LoginFailed",
                }
            );
            await db.SaveChangesAsync();
            return result.Succeeded
                ? Results.Ok(new { user!.ChangePassword })
                : Results.Json(
                    new
                    {
                        code = "credentials",
                        ro = "Date de autentificare invalide.",
                        en = "Invalid credentials.",
                    },
                    statusCode: 401
                );
        }
    )
    .RequireRateLimiting("login");
app.MapPost(
        "/api/auth/logout",
        async (SignInManager<User> sign, HttpContext c, Reports reports, Db db) =>
        {
            reports.Log(c.User, null, "Authentication", "Logout");
            await db.SaveChangesAsync();
            await sign.SignOutAsync();
            return Results.NoContent();
        }
    )
    .RequireAuthorization();
app.MapPost(
        "/api/auth/change-password",
        async (
            PasswordChange i,
            UserManager<User> users,
            SignInManager<User> sign,
            HttpContext c,
            Reports reports,
            Db db
        ) =>
        {
            var u = await users.FindByIdAsync(Reports.Actor(c.User));
            var result = await users.ChangePasswordAsync(u!, i.Current, i.New);
            Guard.Require(
                result.Succeeded,
                "password",
                "Parola nu respectă politica sau parola actuală este incorectă.",
                "Password policy failed or current password is incorrect."
            );
            u!.ChangePassword = false;
            await users.UpdateAsync(u);
            reports.Log(c.User, null, "Authentication", "PasswordChanged");
            await db.SaveChangesAsync();
            await db.Sessions.Where(s => s.UserId == u.Id).ExecuteDeleteAsync();
            await sign.RefreshSignInAsync(u);
            return Results.NoContent();
        }
    )
    .RequireAuthorization();
app.MapGet(
        "/api/me",
        async (HttpContext c, UserManager<User> users) =>
        {
            var u = await users.FindByIdAsync(Reports.Actor(c.User));
            return Results.Ok(
                new
                {
                    u!.Id,
                    u.Name,
                    u.UserName,
                    u.Email,
                    u.Worker,
                    u.ManagerId,
                    u.ChangePassword,
                    roles = await users.GetRolesAsync(u),
                }
            );
        }
    )
    .RequireAuthorization();
var api = app.MapGroup("/api").RequireAuthorization();
Api.Map(api);
Admin.Map(api);
Finance.Map(api);
FinancialCalculations.Map(api);
TaxPolicies.Map(api);
SupplierOcrTemplates.Map(api);
CalculationHistory.Map(api);
ReportResets.Map(api);
BankTreatments.Map(api);
FinanceAcceptance.Map(api);
Documents.Map(api);
VehicleDeductibility.Map(api);
PostingWorkflow.Map(api);
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();

public record Login(string Username, string Password);

public record PasswordChange(string Current, string New);

public partial class Program { }
