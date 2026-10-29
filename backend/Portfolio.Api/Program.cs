using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Controllers;
using Portfolio.Api.Data;
using Portfolio.Api.Infrastructure;
using Portfolio.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---- Database (SQLite). Relative "Data Source" paths resolve against the content root. ----
var connection = new SqliteConnectionStringBuilder(config.GetConnectionString("Default") ?? "Data Source=App_Data/portfolio.db");
if (!connection.DataSource.StartsWith(':') && !connection.DataSource.StartsWith("file:"))
{
    if (!Path.IsPathRooted(connection.DataSource))
        connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
    Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
}
builder.Services.AddDbContext<PortfolioDbContext>(o => o.UseSqlite(connection.ConnectionString));

// ---- Application services ----
builder.Services.Configure<StorageOptions>(config.GetSection("Storage"));
var rateLimits = config.GetSection("RateLimits").Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddSingleton(SmtpOptions.FromConfiguration(config));
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<EmailConfigProvider>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<AdminAccountService>();
builder.Services.AddScoped<PortfolioQueryService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();

// ---- Authentication: HttpOnly, SameSite=Strict session cookie for the single admin ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "portfolio_admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // Reject sessions issued before the last password change.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<PortfolioDbContext>();
            var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
            var stamp = ctx.Principal?.FindFirstValue(AuthController.StampClaim);
            var valid = await db.AdminUsers.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp);
            if (!valid)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

// ---- Rate limiting (per client IP) ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { title = "Too many requests. Please wait a few minutes and try again." }, ct);

    static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    o.AddPolicy(RateLimitPolicies.Login, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateLimits.LoginPermitLimit,
        Window = TimeSpan.FromSeconds(rateLimits.LoginWindowSeconds),
    }));
    o.AddPolicy(RateLimitPolicies.Contact, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c), _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = rateLimits.ContactPermitLimit,
        Window = TimeSpan.FromSeconds(rateLimits.ContactWindowSeconds),
    }));
});

// ---- CORS: the site is served same-origin; extra origins (e.g. a separately hosted frontend) must be listed explicitly ----
var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ---- Reverse proxy support (enable when hosted behind nginx/IIS/a load balancer) ----
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

if (config.GetValue<bool>("ReverseProxy:Enabled")) app.UseForwardedHeaders();
app.UseExceptionHandler();   // ProblemDetails without stack traces
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseStaticFiles(new StaticFileOptions
{
    // Own JS/CSS are not fingerprinted: always revalidate so deployments are picked up. Vendored libs are versioned.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
        ctx.Context.Request.Path.StartsWithSegments("/lib") ? "public, max-age=604800" : "no-cache",
});
// Route after static files: otherwise the catch-all "/{**path}" endpoint claims /css, /js etc. and static files skips them.
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseCsrfHeaderCheck();
app.MapControllers();

app.MapGet("/api/health", async (PortfolioDbContext db) =>
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    return Results.Ok(new { status = pending.Count == 0 ? "ok" : "database-not-migrated", pendingMigrations = pending.Count });
});

await InitializeAsync(app);
app.Run();

// With Database:AutoMigrate (on by default) the SQLite file is created and upgraded on startup.
// Set it to false to apply migrations yourself with `dotnet ef database update`; the app warns until you do.
static async Task InitializeAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<PortfolioDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    var cfg = app.Configuration;

    if (cfg.GetValue<bool>("Database:AutoMigrate")) await MigrateWithRetryAsync(db, logger);

    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        logger.LogWarning("Database has {Count} pending migration(s). Run `dotnet ef database update` in backend/Portfolio.Api, then restart.", pending.Count);
        return;
    }

    await services.GetRequiredService<AdminAccountService>().EnsureInitialAdminAsync(cfg);
    if (cfg.GetValue("Seed:Enabled", true))
        await DbSeeder.SeedAsync(db, services.GetRequiredService<FileStorageService>(), app.Environment, logger);
}

static async Task MigrateWithRetryAsync(PortfolioDbContext db, ILogger logger)
{
    const int maxAttempts = 5;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 5)
        {
            if (attempt == maxAttempts)
            {
                throw new InvalidOperationException("SQLite remained locked after multiple migration attempts. Stop other API instances or processes using App_Data/portfolio.db and restart the API.", ex);
            }

            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            logger.LogWarning(ex, "SQLite is locked while applying migrations. Retrying in {DelaySeconds} seconds (attempt {Attempt}/{MaxAttempts}).", delay.TotalSeconds, attempt, maxAttempts);
            await Task.Delay(delay);
        }
    }
}

public partial class Program;

