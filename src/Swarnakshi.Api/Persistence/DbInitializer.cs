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

        await MigrateOrExplainAsync(db);

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

    /// <summary>
    /// Applies migrations, and turns the two ways this fails on a fresh server into sentences that
    /// name the fix.
    ///
    /// <para>EF decides whether a database exists by opening a connection to it. A database that
    /// exists but whose login has no user inside it therefore looks exactly like one that is not
    /// there — so EF tries to CREATE it, the application's login is deliberately not dbcreator, and
    /// the process dies on "CREATE DATABASE permission denied in database 'master'". That message
    /// sends whoever reads it looking for a permissions problem in master, when what is actually
    /// wrong is one missing CREATE USER in a database that was sitting there the whole time.</para>
    /// </summary>
    private static async Task MigrateOrExplainAsync(AppDbContext db)
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            db.Database.GetConnectionString() ?? "");
        var database = connection.InitialCatalog;
        var login = connection.IntegratedSecurity ? "the service account" : connection.UserID;

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 262 or 5011 or 4060 or 916)
        {
            // 262  CREATE DATABASE permission denied
            // 4060 / 916  cannot open the database requested by the login
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
    }

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
