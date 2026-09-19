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
        AddPersistence(services, config);

        var jwt = new JwtOptions();
        config.GetSection("Jwt").Bind(jwt);
        services.AddSingleton(jwt);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITransactionSequenceService, TransactionSequenceService>();

        // Multi-tenancy: the platform operator and founding tenant defaults, and the provisioner
        // that gives every newly registered company its own copy of the master data.
        services.AddSingleton(ReadPlatformSeedOptions(config));
        services.AddScoped<ICompanyProvisioner, CompanyProvisioner>();
        services.AddSingleton<IRegistrationPolicy>(
            new RegistrationPolicy(int.TryParse(config["Registration:TrialDays"], out var d) ? d : 30));

        var uploadRoot = config["Storage:LocalRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
        services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(uploadRoot));

        return services;
    }

    /// <summary>
    /// SQL Server, and only SQL Server. There is no second provider and no fallback: a connection
    /// string that cannot be reached should stop the application with that fact, not quietly leave
    /// it running against a file nobody is backing up.
    /// </summary>
    private static void AddPersistence(IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conn))
        {
            // Better to stop here with the fix in the message than to start against the wrong
            // database, seed it, and be discovered later.
            throw new InvalidOperationException(
                "No connection string. Set ConnectionStrings:Default — in appsettings.Production.json "
                + "on a server, with `dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"` on a "
                + "developer machine, or via the ConnectionStrings__Default environment variable. "
                + "See docs/06-deployment.md.");
        }

        var commandTimeout = int.TryParse(config["Database:CommandTimeoutSeconds"], out var t) ? t : 60;

        // Deliberately no EnableRetryOnFailure. Posting an approval, issuing material and receiving
        // a purchase each open a transaction with BeginTransactionAsync, and EF refuses a
        // user-initiated transaction while a retrying execution strategy is configured — the app
        // would throw on its most important write path. Turning retries on means first routing all
        // six of those units of work through CreateExecutionStrategy().ExecuteAsync, and making
        // each safe to run twice. That decision is worth revisiting once the database is in the
        // cloud and the network between them is real.
        services.AddDbContext<AppDbContext>(opt => Configure(opt, conn, commandTimeout));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// The one place the provider is named. The design-time factory and the test harness call this
    /// too, so "which database, and how" cannot drift between the app, `dotnet ef` and the tests.
    ///
    /// <para><b>snake_case</b> is the PostgreSQL convention, and this is the one moment it is free:
    /// the schema is being created from scratch, and the data migrator maps names from the model
    /// rather than by hand. Keeping EF's PascalCase would have meant quoting every identifier in
    /// every ad-hoc query for the life of the system — <c>"PurchaseHeaders"."TotalAmount"</c> —
    /// which is exactly the kind of friction that stops people looking at their own data.</para>
    /// </summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder opt, string connectionString, int commandTimeout = 60)
        => opt.UseNpgsql(connectionString, pg => pg.CommandTimeout(commandTimeout))
              .UseSnakeCaseNamingConvention();

    /// <summary>
    /// Binds "Platform", then lets a "PlatformAdmin" section override the operator's credentials.
    /// Two shapes because the deployment settings file names that section the way an operator
    /// thinks of it — the account, not the seeder that creates it.
    /// </summary>
    private static PlatformSeedOptions ReadPlatformSeedOptions(IConfiguration config)
    {
        var options = new PlatformSeedOptions();
        config.GetSection("Platform").Bind(options);

        var admin = config.GetSection("PlatformAdmin");
        if (admin.Exists())
        {
            if (admin["Username"] is { Length: > 0 } u) options.EnterpriseAdminUsername = u;
            if (admin["Password"] is { Length: > 0 } p) options.EnterpriseAdminPassword = p;
            if (admin["DisplayName"] is { Length: > 0 } n) options.EnterpriseAdminDisplayName = n;
        }
        return options;
    }
}
