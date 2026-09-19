using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Swarnakshi.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without spinning up the full web host.
///
/// <para>The connection string only has to be well-formed: adding a migration and generating a
/// script both work from the model in this assembly and never open it. Point it at a scratch
/// database anyway, so a command that does connect cannot touch production by accident.</para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=swarnakshi_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        // Through the same method the application uses, so the migrations `dotnet ef` generates
        // are for the provider and naming convention the application will actually run against.
        DependencyInjection.Configure(options, conn);
        return new AppDbContext(options.Options);
    }
}
