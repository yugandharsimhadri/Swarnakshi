using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Persistence.Seed;

namespace Swarnakshi.Api.Persistence;

/// <summary>Applies migrations, then seeds the platform operator, the founding tenant and its data.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var clock = sp.GetRequiredService<IDateTimeProvider>();
        var platformOptions = sp.GetRequiredService<PlatformSeedOptions>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Swarnakshi.Startup");

        var wait = TimeSpan.FromSeconds(
            int.TryParse(config["Database:StartupWaitSeconds"], out var s) ? s : DefaultStartupWaitSeconds);

        await MigrateOrExplainAsync(db, log, wait);

        // EnterpriseAdmin + the founding company, adopting any pre-tenancy rows into it.
        var companyId = await PlatformSeeder.RunAsync(db, hasher, platformOptions, clock.Today);

        // Master data belongs to a company now, so it is seeded inside that company's scope.
        using (db.BeginTenantScope(companyId))
        {
            await MasterDataSeeder.RunAsync(db);

            if (isDevelopment && bool.TryParse(config["Seed:Demo"], out var demo) && demo)
                await DemoDataSeeder.RunAsync(db);
        }

        // Every other company was provisioned once, at registration, and never seeded again — so a
        // change to the shape of the taxonomy would reach the founding tenant and no one else.
        // The seeder is idempotent and does nothing when a tenant is already current, so running it
        // per company on startup is cheap and keeps them all on the same tree.
        await MigrateTenantTaxonomiesAsync(db, companyId);
    }

    /// <summary>How long to keep trying to reach the server before giving up. See <see cref="MigrateOrExplainAsync"/>.</summary>
    private const int DefaultStartupWaitSeconds = 60;

    /// <summary>
    /// PostgreSQL's ways of saying "the server answered, and this is your problem, not a timing
    /// one" — waiting changes nothing, so these stop immediately with the fix in the message.
    /// </summary>
    private static bool IsRefusal(PostgresException ex) => ex.SqlState is
        PostgresErrorCodes.InvalidCatalogName          // 3D000  database does not exist
        or PostgresErrorCodes.InvalidPassword          // 28P01
        or PostgresErrorCodes.InvalidAuthorizationSpecification   // 28000  no such role, or pg_hba refused it
        or PostgresErrorCodes.InsufficientPrivilege;   // 42501  can connect, cannot CREATE DATABASE / CREATE TABLE

    /// <summary>
    /// "There was nothing at that address to talk to yet." 57P03 is PostgreSQL itself saying it is
    /// still starting up — the exact shape of a reboot race — and the rest are the socket layer:
    /// connection refused, host unreachable, timed out.
    /// </summary>
    private static bool IsUnreachable(NpgsqlException ex) =>
        ex is PostgresException pg
            ? pg.SqlState is PostgresErrorCodes.CannotConnectNow
                or PostgresErrorCodes.SqlClientUnableToEstablishSqlConnection
                or PostgresErrorCodes.ConnectionFailure
            : ex.IsTransient || ex.InnerException is System.Net.Sockets.SocketException or TimeoutException or IOException;

    /// <summary>
    /// Applies migrations, waiting for the server to appear, and turning the ways this fails on a
    /// real deployment into sentences that name the fix.
    ///
    /// <para><b>Waiting matters more than it sounds.</b> The app pool runs AlwaysRunning, so on a
    /// Windows reboot IIS starts this process as soon as it can — which can be before PostgreSQL
    /// is accepting connections. Failing on the first refusal turned a few seconds of boot
    /// ordering into an outage that lasted until somebody ran iisreset, because the process died,
    /// and a process that has died cannot notice the database arriving a moment later. It retries
    /// for a minute, which costs a misconfigured server one minute before it fails just as
    /// loudly, and costs a correctly configured one nothing at all.</para>
    ///
    /// <para>EF decides whether a database exists by opening a connection to it, and if that fails
    /// it tries to CREATE it. The application's role is deliberately not allowed to, so a database
    /// that was never created, a role that cannot log in, and a role that can log in but owns
    /// nothing all end the same way: "permission denied". The message below names all three and
    /// the one script that fixes any of them.</para>
    /// </summary>
    private static async Task MigrateOrExplainAsync(AppDbContext db, ILogger log, TimeSpan wait)
    {
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString() ?? "");
        var database = connection.Database ?? "(unset)";
        var server = string.IsNullOrWhiteSpace(connection.Host) ? "(unset)" : $"{connection.Host}:{connection.Port}";
        var login = connection.Username ?? "(unset)";

        var deadline = DateTimeOffset.UtcNow + wait;
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                await db.Database.MigrateAsync();
                if (attempt > 1)
                    log.LogInformation(
                        "PostgreSQL at {Server} became reachable on attempt {Attempt}; the schema is up to date.",
                        server, attempt);
                return;
            }
            catch (PostgresException ex) when (IsRefusal(ex))
            {
                throw new InvalidOperationException(
                    $"""
                     PostgreSQL at '{server}' refused to open database '{database}' as role '{login}'
                     ({ex.SqlState}: {FirstLine(ex.MessageText)}).

                     Depending on which of these it is, the database was never created, the role
                     cannot log in (wrong password, or pg_hba.conf does not allow it from here), or
                     the role can log in but is not the owner of the database. One script fixes all
                     three and is safe to re-run:

                       psql -U postgres -h localhost -v DbName="{database}" -v AppRole="{login}" -v AppPassword="<password>" -f 01-create-database.sql

                     See docs/11-postgresql.md, step 2.
                     """, ex);
            }
            catch (NpgsqlException ex)
                when (IsUnreachable(ex) && DateTimeOffset.UtcNow + RetryPause < deadline)
            {
                // Warning, not Error: this is the expected shape of a boot, and logging it as a
                // failure would train whoever reads the log to ignore the line that matters.
                log.LogWarning(
                    "PostgreSQL at {Server} is not reachable yet (attempt {Attempt}: {Reason}). "
                    + "Retrying for up to {Remaining:N0} more seconds.",
                    server, attempt, FirstLine(ex.Message), (deadline - DateTimeOffset.UtcNow).TotalSeconds);
                await Task.Delay(RetryPause);
            }
            catch (NpgsqlException ex) when (IsUnreachable(ex))
            {
                throw new InvalidOperationException(
                    $"""
                     PostgreSQL at '{server}' did not answer within {wait.TotalSeconds:N0} seconds, over
                     {attempt} attempt(s), so this application cannot start.

                     Nothing answered at that address — this is not a password or a permissions
                     problem. The usual causes, in order:

                       1. The PostgreSQL service is not running. Check it:
                            Get-Service postgresql*
                          If this happened at boot, PostgreSQL was simply slower than IIS. Raise
                          Database:StartupWaitSeconds if this machine is slow to come up.

                       2. The host or port is wrong. '{server}' must be where PostgreSQL listens;
                          the default port is 5432, and 'localhost' is this machine.

                       3. PostgreSQL is not listening on that address. listen_addresses in
                          postgresql.conf decides, and pg_hba.conf decides who may connect.

                     Last error: {FirstLine(ex.Message)}
                     """, ex);
            }
        }
    }

    private static readonly TimeSpan RetryPause = TimeSpan.FromSeconds(3);

    /// <summary>Driver messages can run to several lines; the first one carries the diagnosis.</summary>
    private static string FirstLine(string message) =>
        message.Split('\n')[0].Trim();

    private static async Task MigrateTenantTaxonomiesAsync(AppDbContext db, Guid foundingCompanyId)
    {
        var others = await db.Companies.AsNoTracking()
            .Where(c => c.Id != foundingCompanyId)
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var id in others)
        {
            using var tenant = db.BeginTenantScope(id);
            await MaterialMasterSeeder.RunAsync(db);
        }
    }
}
