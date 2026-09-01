using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Platform;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Persistence.Seed;
using Swarnakshi.Infrastructure.Services;
using Swarnakshi.Infrastructure.Storage;

namespace Swarnakshi.Infrastructure;

/// <summary>Trial length for a self-registered company, before an EnterpriseAdmin renews it.</summary>
public sealed record RegistrationPolicy(int TrialDays) : IRegistrationPolicy;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "Sqlite";
        var conn = config.GetConnectionString("Default") ?? "Data Source=swarnakshi.db";

        services.AddDbContext<AppDbContext>(opt =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                opt.UseSqlServer(conn);
            else
                opt.UseSqlite(conn);
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var jwt = new JwtOptions();
        config.GetSection("Jwt").Bind(jwt);
        services.AddSingleton(jwt);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITransactionSequenceService, TransactionSequenceService>();

        // Multi-tenancy: the platform operator and founding tenant defaults, and the provisioner
        // that gives every newly registered company its own copy of the master data.
        var platformSeed = new PlatformSeedOptions();
        config.GetSection("Platform").Bind(platformSeed);
        services.AddSingleton(platformSeed);
        services.AddScoped<ICompanyProvisioner, CompanyProvisioner>();
        services.AddSingleton<IRegistrationPolicy>(
            new RegistrationPolicy(int.TryParse(config["Registration:TrialDays"], out var d) ? d : 30));

        var uploadRoot = config["Storage:LocalRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
        services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(uploadRoot));

        return services;
    }
}
