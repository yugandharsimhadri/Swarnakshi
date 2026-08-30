using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Services;
using Swarnakshi.Infrastructure.Storage;

namespace Swarnakshi.Infrastructure;

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

        var uploadRoot = config["Storage:LocalRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
        services.AddSingleton<IFileStorage>(_ => new LocalFileStorage(uploadRoot));

        return services;
    }
}
