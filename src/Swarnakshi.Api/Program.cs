using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    // process's working directory. A Windows Service starts in C:WindowsSystem32, and an
    // operator may launch the exe from anywhere; either way the host would otherwise look for
    // appsettings.Production.json and wwwroot in the wrong place and quietly find neither.
    ContentRootPath = AppContext.BaseDirectory
});

// Lets the published app be installed with New-Service and started by Windows at boot. It is a
// no-op when the process is launched from a console or by `dotnet run`, so this one line covers
// both the developer's machine and the server. See docs/06-deployment.md.
builder.Host.UseWindowsService(o => o.ServiceName = "Swarnakshi");

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

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive docs at /scalar/v1
}

// The published site serves the built React app out of wwwroot, so the UI and the API share one
// origin and the client's relative /api calls just work — no reverse proxy, no CORS in production,
// one process to install and watch. In development wwwroot does not exist and Vite proxies instead.
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
