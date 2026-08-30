using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Swarnakshi.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` build the context without spinning up the full web host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Sqlite";
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Data Source=swarnakshi.db";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            options.UseSqlServer(conn);
        else
            options.UseSqlite(conn);

        return new AppDbContext(options.Options);
    }
}
