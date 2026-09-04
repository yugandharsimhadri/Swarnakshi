using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Swarnakshi.Api.Common;
using Swarnakshi.Api.Persistence;
using Swarnakshi.Application;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure;
using Swarnakshi.Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Pin the content root to the folder the binaries are in, rather than letting it follow the
    // process's working directory. A Windows Service starts in C:\Windows\System32, and an
    // operator may launch the exe from anywhere; either way the host would otherwise look for
    // appsettings.Production.json and wwwroot in the wrong place and quietly find neither.
    ContentRootPath = AppContext.BaseDirectory
});

// Lets the published app be installed with New-Service and started by Windows at boot. It is a
// no-op when the process is launched from a console or by `dotnet run`, so this one line covers
// both the developer's machine and the server. See docs/06-deployment.md.
builder.Host.UseWindowsService(o => o.ServiceName = "Swarnakshi");

// Logging, before anything else can fail.
//
// A Windows service writes its console output to nowhere and an IIS worker much the same, so
// without a file on disk an exception in production leaves nothing behind to read. Rolling daily,
// kept for a fortnight, next to the data rather than inside the app folder — a deployment replaces
// that folder, and the logs explaining why the last one went wrong should survive it.
var logDirectory = builder.Configuration["Logging:Directory"]
    ?? Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? ".",
                    "logs");
Directory.CreateDirectory(logDirectory);

builder.Host.UseSerilog((context, services, cfg) => cfg
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    // EF logs every SQL statement at Information, which would bury the entries worth reading.
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDirectory, "swarnakshi-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        // A runaway loop must not fill the disk and take the database down with it.
        fileSizeLimitBytes: 50L * 1024 * 1024,
        rollOnFileSizeLimit: true,
        shared: true,
        // Flush every second rather than only on a clean shutdown. A process that is killed - a
        // crash, an app-pool recycle, someone ending the task - never gets to flush, and the lines
        // lost would be the ones written just before it went: exactly the ones worth reading.
        // The cost is a disk write a second on an idle server.
        flushToDiskInterval: TimeSpan.FromSeconds(1),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwt);
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:Key must be set (>=32 chars) outside Development.");
    jwt.Key = "dev-only-insecure-signing-key-change-me-please-32+";
}
builder.Services.AddSingleton(jwt); // authoritative — overrides the Infrastructure default

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

const string CorsPolicy = "web";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:6050"])
    .AllowAnyHeader().AllowAnyMethod()));

// Behind a reverse proxy or a Cloudflare tunnel, the request that reaches Kestrel is plain HTTP
// from localhost — the TLS ended at the edge. Without this the app believes it is serving
// http://localhost, and anything it derives from the request (a scheme in a link, a redirect, the
// host it logs) is wrong. The proxy is on this machine and reached only over the loopback, so no
// list of known proxies is needed; clearing the defaults is what allows that.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Sign-in is the one anonymous endpoint that gives something away when guessed, and it is now
// reachable from the internet. Ten attempts a minute per address is generous for a person typing a
// password they half-remember and useless to anyone working through a word list.
//
// Partitioning by IP only works because UseForwardedHeaders runs first and puts the caller's real
// address on the connection; without it every request through the tunnel shares one partition and
// the first person to fumble a password locks out the whole company.
// "auth" is the name the [EnableRateLimiting] attributes on the sign-in, refresh and registration
// endpoints refer to. Only those carry it: a limit this tight on ordinary traffic would throttle a
// person simply using the app.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = int.TryParse(builder.Configuration["Security:AuthAttemptsPerMinute"], out var n) ? n : 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,      // reject rather than delay: a queued sign-in just looks broken
        }));
});

var app = builder.Build();

// First in the pipeline: everything after it should see the scheme and host the caller actually used.
app.UseForwardedHeaders();

// Cheap headers that close off whole classes of browser attack. Deliberately not a Content-Security
// -Policy: the UI is served from here in one deployment shape and from Cloudflare in the other, so
// the correct policy differs, and a wrong CSP breaks the app silently. Set that at the edge.
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";          // stop MIME sniffing turning data into script
    h["X-Frame-Options"] = "DENY";                    // no framing, so no clickjacking
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRateLimiter();

// One line per request, with the outcome and how long it took. The detail of a failure comes from
// ExceptionMiddleware; this is what tells you a request happened at all.
app.UseSerilogRequestLogging(o =>
{
    o.GetLevel = (http, elapsed, ex) =>
        ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Response.StatusCode >= 400 ? LogEventLevel.Warning
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose  // the tunnel polls this
        : LogEventLevel.Information;

    o.EnrichDiagnosticContext = (diagnostic, http) =>
    {
        diagnostic.Set("User", http.User.Identity?.Name ?? "anonymous");
        diagnostic.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString() ?? "-");
    };
});

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive docs at /scalar/v1
}

// The published site serves the built React app out of wwwroot, so a browser on the site's own
// hostname makes only same-origin calls and never needs CORS. A second hostname pointed at the
// same process — an api. name for integrations, say — is served by this too. Cors:Origins is for
// the remaining case: some OTHER site calling this API from a browser.
// In development wwwroot does not exist and Vite proxies instead.
if (app.Environment.WebRootPath is { Length: > 0 } webRoot && Directory.Exists(webRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// A request under /api that matched no controller is a missing endpoint, and must say so. Without
// this it would reach the SPA fallback below and answer an API client with a page of HTML and a 200.
app.MapFallback("/api/{**rest}", () => Results.NotFound(new
{
    success = false, message = "No such endpoint.", data = (object?)null, errors = Array.Empty<string>()
}));

// Deep links (/projects/<id>, /inventory/purchases/new) are client-side routes: the server has no
// file at that path and must hand back the shell so React Router can resolve it.
if (app.Environment.WebRootPath is { Length: > 0 } root && File.Exists(Path.Combine(root, "index.html")))
    app.MapFallbackToFile("index.html");

// Deployment runs the schema change as its own step —  `Swarnakshi.Api.exe --migrate`  — so a bad
// migration fails the deploy with a non-zero exit code, before the site is swapped in and starts
// taking traffic. Without the switch this is the ordinary startup path and behaves as it always did.
var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);

await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Environment.IsDevelopment());

if (migrateOnly)
{
    app.Logger.LogInformation("Migrations applied and seed data verified. Exiting (--migrate).");
    return;
}

app.Run();
