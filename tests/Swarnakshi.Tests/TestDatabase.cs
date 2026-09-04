using Microsoft.Data.SqlClient;

namespace Swarnakshi.Tests;

/// <summary>
/// The SQL Server database the whole test assembly runs against.
///
/// <para>One database, created once, holding the schema and nothing else. Each <see cref="TestHost"/>
/// then gets its own <em>tenant</em> inside it, which is the isolation the product itself relies on:
/// every tenant row carries a CompanyId, a global query filter scopes reads to it, and SaveChanges
/// stamps writes. A test that could see another test's rows would be a tenancy bug worth failing
/// over, so running them together is a check rather than a compromise.</para>
///
/// <para>Why not a database per test: creating one, building 43 tables in it and dropping it again
/// costs seconds, and there are over two hundred hosts. That is the difference between a suite
/// people run and one they skip.</para>
///
/// <para>The name carries the process id, so two suites running at once - a developer and an IDE
/// test runner, or two CI jobs on one agent - never share a database. It is dropped when the
/// assembly finishes.</para>
/// </summary>
public static class TestDatabase
{
    private static readonly Lock Gate = new();
    private static string? _name;

    /// <summary>Where test databases are created. Overridable for an agent whose instance differs.</summary>
    private static string Instance =>
        Environment.GetEnvironmentVariable("SWARNAKSHI_TEST_SQL_SERVER") ?? @".\SQLEXPRESS";

    public static string Name
    {
        get
        {
            lock (Gate)
            {
                return _name ??= $"SwarnakshiTest_{Environment.ProcessId}_{DateTime.Now:HHmmss}";
            }
        }
    }

    public static string ConnectionString => For(Name);

    /// <summary>A database of this run's own, for a test that needs the whole database to itself.</summary>
    public static async Task<string> CreateOwnAsync()
    {
        // Math.Min, not a bare [..60]: the name is only about 59 characters when the process id is
        // four digits, and slicing past the end threw. It failed on some runs and not others for no
        // reason a reader could see, because what varied was the width of the pid.
        var candidate = $"{Name}_{Guid.NewGuid():N}";
        var name = candidate[..Math.Min(candidate.Length, 100)];   // sysname allows 128
        await using var connection = new SqlConnection(For("master"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}];";
        await command.ExecuteNonQueryAsync();
        return name;
    }

    public static async Task DropOwnAsync(string name)
    {
        try
        {
            await using var connection = new SqlConnection(For("master"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{name}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{name}];
                END
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch { /* swept up by the next run; see CreateAsync */ }
    }

    public static string ConnectionStringFor(string database) => For(database);

    private static string For(string database) =>
        $"Server={Instance};Database={database};Trusted_Connection=True;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=False;Application Name=Swarnakshi.Tests";

    /// <summary>
    /// Creates this run's database, and sweeps up any left by a run that was killed before it could
    /// tidy up after itself. The sweep is what makes teardown a matter of tidiness rather than
    /// correctness: stopping a run mid-way costs a few megabytes until the next one, not a
    /// gradually filling instance.
    /// </summary>
    public static async Task CreateAsync()
    {
        await using var connection = new SqlConnection(For("master"));
        await connection.OpenAsync();

        await SweepAsync(connection);

        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{Name}];";
        await create.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Drops test databases whose run is over.
    ///
    /// <para>The name carries the process id that made it, so "is that run still going?" has an
    /// exact answer: look for the process. That beats guessing from age — an age rule either
    /// leaves same-day leftovers lying around, which is what happened (eighteen of them after a
    /// day's work), or risks dropping a database out from under a run that is simply slow.</para>
    ///
    /// <para>A test runner does not always let the process exit cleanly, so teardown cannot be the
    /// only cleanup. This is the one that actually holds.</para>
    /// </summary>
    private static async Task SweepAsync(SqlConnection connection)
    {
        var stale = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT name FROM sys.databases WHERE name LIKE 'SwarnakshiTest[_]%';";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (name == Name || IsFromADeadRun(name)) stale.Add(name);
            }
        }

        foreach (var name in stale)
        {
            try
            {
                await using var drop = connection.CreateCommand();
                drop.CommandText = $"""
                    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{name}];
                    """;
                await drop.ExecuteNonQueryAsync();
            }
            catch { /* in use after all, or already gone; the next run tries again */ }
        }
    }

    /// <summary>SwarnakshiTest_&lt;pid&gt;_&lt;time&gt;[_&lt;guid&gt;] — true when that pid is gone.</summary>
    private static bool IsFromADeadRun(string databaseName)
    {
        var parts = databaseName.Split('_');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var pid)) return false;
        if (pid == Environment.ProcessId) return false;
        try { using var _ = System.Diagnostics.Process.GetProcessById(pid); return false; }
        catch (ArgumentException) { return true; }      // no such process: the run is over
        catch { return false; }                          // cannot tell; leave it alone
    }

    public static async Task DropAsync()
    {
        try
        {
            SqlConnection.ClearAllPools();      // else our own pooled connections block the DROP
            await using var connection = new SqlConnection(For("master"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{Name}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{Name}];
                END
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // A leftover test database is a few megabytes named for the process that made it.
            // Not worth failing a green run over.
        }
    }
}
