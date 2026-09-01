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

        await db.Database.MigrateAsync();

        // EnterpriseAdmin + the founding company, adopting any pre-tenancy rows into it.
        var companyId = await PlatformSeeder.RunAsync(db, hasher, platformOptions, clock.Today);

        // Master data belongs to a company now, so it is seeded inside that company's scope.
        using (db.BeginTenantScope(companyId))
        {
            await MasterDataSeeder.RunAsync(db);

            if (isDevelopment && bool.TryParse(config["Seed:Demo"], out var demo) && demo)
                await DemoDataSeeder.RunAsync(db);
        }
    }
}
