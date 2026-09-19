using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swarnakshi.Infrastructure.Persistence;

namespace Swarnakshi.Tests;

/// <summary>
/// The PostgreSQL database the whole test assembly runs against.
///
/// <para>One database, created once, holding the schema and nothing else. Each <see cref="TestHost"/>
/// then gets its own <em>tenant</em> inside it, which is the isolation the product itself relies on:
/// every tenant row carries a CompanyId, a global query filter scopes reads to it, and SaveChanges
/// stamps writes. A test that could see another test's rows would be a tenancy bug worth failing
/// over, so running them together is a check rather than a compromise.</para>
///
/// <para>Why not a database per test: creating one, building 44 tables in it and dropping it again
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

    /// <summary>
    /// How to reach the server, as a connection string WITHOUT a database — the run picks its own.
    /// From testsettings.json at the repository root; see <see cref="TestSettings"/>.
    /// </summary>
    private static string Server => TestSettings.Postgres;

    public static string Name
    {
        get
        {
            lock (Gate)
            {
                // Lower-case, because PostgreSQL folds unquoted identifiers and a mixed-case name
                // would need quoting in every psql command anybody ever typed against it.
                return _name ??= $"swarnakshi_test_{Environment.ProcessId}_{DateTime.Now:HHmmss}";
            }
        }
    }

    public static string ConnectionString => For(Name);

    /// <summary>A database of this run's own, for a test that needs the whole database to itself.</summary>
    public static async Task<string> CreateOwnAsync()
    {
        // PostgreSQL identifiers are capped at 63 bytes; the guid is trimmed to fit under it.
        var name = $"{Name}_{Guid.NewGuid():N}"[..Math.Min(63, Name.Length + 33)];
        await using var connection = new NpgsqlConnection(For("postgres"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{name}\";";
        await command.ExecuteNonQueryAsync();
        return name;
    }

    public static async Task DropOwnAsync(string name)
    {
        try { await DropDatabaseAsync(name); }
        catch { /* swept up by the next run; see CreateAsync */ }
    }

    public static string ConnectionStringFor(string database) => For(database);

    /// <summary>Context options for a database of this run, configured exactly as the application configures its own.</summary>
    public static DbContextOptions<AppDbContext> Options(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Swarnakshi.Infrastructure.DependencyInjection.Configure(builder, connectionString);
        return builder.Options;
    }

    private static string For(string database)
    {
        var b = new NpgsqlConnectionStringBuilder(Server)
        {
            Database = database,
            ApplicationName = "Swarnakshi.Tests",
            // Pooled connections outlive the test that opened them and block DROP DATABASE. The
            // suite opens thousands of short-lived connections; the pool stays, but is cleared
            // before every drop.
            Pooling = true,
        };
        return b.ConnectionString;
    }

    /// <summary>
    /// Creates this run's database, and sweeps up any left by a run that was killed before it could
    /// tidy up after itself. The sweep is what makes teardown a matter of tidiness rather than
    /// correctness: stopping a run mid-way costs a few megabytes until the next one, not a
    /// gradually filling server.
    /// </summary>
    public static async Task CreateAsync()
    {
        await using var connection = new NpgsqlConnection(For("postgres"));
        await connection.OpenAsync();

        await SweepAsync(connection);

        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{Name}\";";
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
    private static async Task SweepAsync(NpgsqlConnection connection)
    {
        var stale = new List<string>();
        await using (var list = connection.CreateCommand())
        {
            list.CommandText = "SELECT datname FROM pg_database WHERE datname LIKE 'swarnakshi\\_test\\_%';";
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (name == Name || IsFromADeadRun(name)) stale.Add(name);
            }
        }

        foreach (var name in stale)
        {
            try { await DropDatabaseAsync(name, connection); }
            catch { /* in use after all, or already gone; the next run tries again */ }
        }
    }

    /// <summary>swarnakshi_test_&lt;pid&gt;_&lt;time&gt;[_&lt;guid&gt;] — true when that pid is gone.</summary>
    private static bool IsFromADeadRun(string databaseName)
    {
        var parts = databaseName.Split('_');
        if (parts.Length < 4 || !int.TryParse(parts[2], out var pid)) return false;
        if (pid == Environment.ProcessId) return false;
        try { using var _ = System.Diagnostics.Process.GetProcessById(pid); return false; }
        catch (ArgumentException) { return true; }      // no such process: the run is over
        catch { return false; }                          // cannot tell; leave it alone
    }

    public static async Task DropAsync()
    {
        try { await DropDatabaseAsync(Name); }
        catch
        {
            // A leftover test database is a few megabytes named for the process that made it.
            // Not worth failing a green run over.
        }
    }

    /// <summary>
    /// PostgreSQL will not drop a database anyone is connected to, and there is no SINGLE_USER to
    /// force it. So: our own pool is cleared, every other session on it is terminated, and only
    /// then is it dropped. FORCE does the last two in one on PostgreSQL 13+, and is used because
    /// the terminate-then-drop dance still races with a reconnecting pool.
    /// </summary>
    private static async Task DropDatabaseAsync(string name, NpgsqlConnection? existing = null)
    {
        NpgsqlConnection.ClearAllPools();

        var connection = existing ?? new NpgsqlConnection(For("postgres"));
        try
        {
            if (existing is null) await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (existing is null) await connection.DisposeAsync();
        }
    }
}
