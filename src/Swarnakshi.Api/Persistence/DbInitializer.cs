using Microsoft.EntityFrameworkCore;
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
    /// SqlClient's way of saying "there was nothing at that address to talk to" — as opposed to
    /// "the server answered and refused you", which is a different problem and never worth a retry.
    /// -2 and -1 are timeouts; 53, 10060, 10061 and 11001 are the socket and name-resolution
    /// failures; 26 arrives as one of these with the provider detail in the message.
    /// </summary>
    private static readonly int[] ServerUnreachable =
        [-2, -1, 2, 20, 53, 64, 233, 258, 10053, 10054, 10060, 10061, 11001];

    /// <summary>
    /// Applies migrations, waiting for the server to appear, and turning the ways this fails on a
    /// real deployment into sentences that name the fix.
    ///
    /// <para><b>Waiting matters more than it sounds.</b> The app pool runs AlwaysRunning, so on a
    /// Windows reboot IIS starts this process as soon as it can — which can be before SQL Server
    /// Express is accepting connections. Failing on the first refusal turned a few seconds of boot
    /// ordering into an outage that lasted until somebody ran iisreset, because the process died,
    /// and a process that has died cannot notice the database arriving a moment later. It now
    /// retries for a minute, which costs a misconfigured server one minute before it fails just as
    /// loudly, and costs a correctly configured one nothing at all.</para>
    ///
    /// <para>EF decides whether a database exists by opening a connection to it. A database that
    /// exists but whose login has no user inside it therefore looks exactly like one that is not
    /// there — so EF tries to CREATE it, the application's login is deliberately not dbcreator, and
    /// the process dies on "CREATE DATABASE permission denied in database 'master'". That message
    /// sends whoever reads it looking for a permissions problem in master, when what is actually
    /// wrong is one missing CREATE USER in a database that was sitting there the whole time.</para>
    /// </summary>
    private static async Task MigrateOrExplainAsync(AppDbContext db, ILogger log, TimeSpan wait)
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            db.Database.GetConnectionString() ?? "");
        var database = connection.InitialCatalog;
        var server = string.IsNullOrWhiteSpace(connection.DataSource) ? "(unset)" : connection.DataSource;
        var login = connection.IntegratedSecurity ? "the service account" : connection.UserID;

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
                        "SQL Server at {Server} became reachable on attempt {Attempt}; the schema is up to date.",
                        server, attempt);
                return;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 262 or 5011 or 4060 or 916)
            {
                // 262  CREATE DATABASE permission denied
                // 4060 / 916  cannot open the database requested by the login
                // The server answered, so waiting changes nothing — say what is wrong and stop.
                throw new InvalidOperationException(
                    $"""
                     Cannot open the database '{database}' as '{login}', and this application is not
                     permitted to create one.

                     If '{database}' does not exist yet, create it. If it does exist, then '{login}' has
                     no user inside it — which looks identical to a missing database from here, and is
                     the more common of the two.

                     Either way, one command fixes both:

                       sqlcmd -S <server> -E -C -b -i 01-create-database.sql -v DbName="{database}" -v AppLogin="{login}" -v AppPassword="<password>"

                     It is idempotent, so running it against a database that already exists only adds
                     what is missing. See docs/06b-deployment-split.md, step 2.
                     """, ex);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
                when (ServerUnreachable.Contains(ex.Number) && DateTimeOffset.UtcNow + RetryPause < deadline)
            {
                // Warning, not Error: this is the expected shape of a boot, and logging it as a
                // failure would train whoever reads the log to ignore the line that matters.
                log.LogWarning(
                    "SQL Server at {Server} is not reachable yet (attempt {Attempt}: {Reason}). "
                    + "Retrying for up to {Remaining:N0} more seconds.",
                    server, attempt, FirstLine(ex.Message), (deadline - DateTimeOffset.UtcNow).TotalSeconds);
                await Task.Delay(RetryPause);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ServerUnreachable.Contains(ex.Number))
            {
                throw new InvalidOperationException(
                    $"""
                     SQL Server at '{server}' did not answer within {wait.TotalSeconds:N0} seconds, over
                     {attempt} attempt(s), so this application cannot start.

                     Nothing answered at that address — this is not a password or a permissions
                     problem. The usual causes, in order:

                       1. The SQL Server service is not running. Check it:
                            Get-Service 'MSSQL$SQLEXPRESS'
                          If this happened at boot, SQL was simply slower than IIS. Set the service
                          to Automatic (not Automatic (Delayed Start)) so it wins that race, and
                          raise Database:StartupWaitSeconds if this machine is slow to come up.

                       2. The instance name is wrong. '{server}' must match what is installed;
                          a default instance is '.' or the machine name, not '.\SQLEXPRESS'.

                       3. TCP/IP is disabled for the instance, or the SQL Server Browser service is
                          stopped, if anything connects to it over the network.

                     Last error: {FirstLine(ex.Message)}
                     """, ex);
            }
        }
    }

    private static readonly TimeSpan RetryPause = TimeSpan.FromSeconds(3);

    /// <summary>SqlClient's messages run to several lines; the first one carries the diagnosis.</summary>
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
