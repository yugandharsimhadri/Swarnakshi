using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure.Persistence;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("Swarnakshi.Tests.TestFrameworkWithDatabase", "Swarnakshi.Tests")]

namespace Swarnakshi.Tests;

/// <summary>
/// Creates the assembly's SQL Server database and builds its schema once, before any test runs,
/// then drops it when the last one finishes.
///
/// <para>xUnit v2 has no assembly-level fixture, so this is the hook it does offer: a test
/// framework the runner constructs once and disposes at the end. Everything about that is
/// incidental — what matters is that CREATE DATABASE and 43 CREATE TABLEs happen once per run
/// rather than once per <see cref="TestHost"/>. There are over two hundred hosts; paying seconds
/// for each would turn a forty-second suite into a coffee break, and a suite people skip catches
/// nothing.</para>
/// </summary>
public sealed class TestFrameworkWithDatabase : XunitTestFramework
{
    public TestFrameworkWithDatabase(IMessageSink messageSink) : base(messageSink)
    {
        TestDatabase.CreateAsync().GetAwaiter().GetResult();
        BuildSchema();

        // xUnit v2's TestFramework.Dispose is not virtual, so the end of the run is the hook left.
        // A run killed outright skips this, which is why CreateAsync also sweeps up databases left
        // by earlier runs — teardown is tidiness here, not correctness.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TestDatabase.DropAsync().GetAwaiter().GetResult();
    }

    private static void BuildSchema()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(TestDatabase.ConnectionString));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // EnsureCreated, not Migrate: these tests are about the rules the model describes, and
        // stamping it on directly is much faster than replaying migrations. Whether the migrations
        // reproduce the same shape is a deployment question, answered by deploy/sql/03-schema.sql.
        db.Database.EnsureCreated();
    }

}
