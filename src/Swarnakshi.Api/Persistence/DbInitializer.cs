using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Persistence.Seed;

namespace Swarnakshi.Api.Persistence;

/// <summary>Applies migrations and seeds master data + the Owner user on startup.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        var email = config["Seed:OwnerEmail"] ?? "owner@swarnakshi.local";
        var password = config["Seed:OwnerPassword"] ?? "Owner@123";
        await MasterDataSeeder.RunAsync(db, hasher, email, password);

        if (isDevelopment && bool.TryParse(config["Seed:Demo"], out var demo) && demo)
            await DemoDataSeeder.RunAsync(db);
    }
}
