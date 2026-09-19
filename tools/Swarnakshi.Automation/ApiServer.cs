using System.Diagnostics;

namespace Swarnakshi.Automation;

/// <summary>
/// Brings up the Swarnakshi API for a UAT run, against a database created fresh for that run.
///
/// The throwaway database is the important part: the suite signs in, creates materials, posts
/// purchases and issues stock. Pointed at the developer's SCOPS it would leave that data behind,
/// and its assertions would be at the mercy of whatever was already there.
///
/// It is a real SQL Server database on the local instance, not a file — the product runs on SQL
/// Server, and an acceptance suite that proves it works on a different engine has proved the wrong
/// thing. EF creates the database on first migrate and this drops it at the end of the run.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;
    private readonly string? _databaseName;

    /// <summary>
    /// The PostgreSQL server UAT databases are created on, as a connection string without a
    /// database. Read from testsettings.json at the repository root — the same file the unit
    /// tests use, so one password lives in one place — with an environment variable as the
    /// fallback for a build agent.
    /// </summary>
    private static string Server
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var file = Path.Combine(dir.FullName, "testsettings.json");
                if (File.Exists(file))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                    if (doc.RootElement.TryGetProperty("Postgres", out var v) && v.GetString() is { Length: > 0 } cs)
                        return cs;
                }
                dir = dir.Parent;
            }
            return Environment.GetEnvironmentVariable("SWARNAKSHI_UAT_POSTGRES")
                ?? throw new InvalidOperationException(
                    "No testsettings.json found in any parent folder and SWARNAKSHI_UAT_POSTGRES is not set. "
                    + "Copy testsettings.template.json to testsettings.json at the repository root and fill it in.");
        }
    }

    private static string ConnectionFor(string database) =>
        new Npgsql.NpgsqlConnectionStringBuilder(Server) { Database = database, ApplicationName = "Swarnakshi.Uat" }
            .ConnectionString;

    private ApiServer(Process? ownedProcess, string? databaseName)
    {
        _ownedProcess = ownedProcess;
        _databaseName = databaseName;
    }

    /// <summary>
    /// The configuration this assembly was built in — which is also the one the API was built in,
    /// since the UAT project's build-order reference compiles it as part of the same build.
    /// </summary>
    private static string BuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    public static async Task<ApiServer> StartAsync(
        AutomationOptions options,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var apiBase = options.ApiBaseUrl.TrimEnd('/');

        if (await ManagedProcess.IsRespondingAsync($"{apiBase}/health", ct))
        {
            if (!options.ManageServers)
            {
                log?.Invoke($"Reusing the API already serving {apiBase}");
                return new ApiServer(null, null);
            }

            throw new InvalidOperationException(
                $"Something is already serving {apiBase}. The UAT run wants its own API on that port " +
                "so it can use a throwaway database. Stop it, or set SWARNAKSHI_UAT_API_BASE_URL.");
        }

        if (!options.ManageServers)
            throw new InvalidOperationException(
                $"Nothing is serving {apiBase} and SWARNAKSHI_UAT_MANAGE_SERVERS=false, so the " +
                "automation will not start one.");

        var port = new Uri(apiBase).Port;
        ManagedProcess.EnsurePortAvailable(port, "API");

        // A name unique to this run, so two runs on one machine cannot collide and a leftover
        // database from a crashed run is never picked up by the next one.
        var databaseName = $"swarnakshi_uat_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..40];

        log?.Invoke($"Starting the API on {apiBase} against a throwaway database");

        // The child's own output is the only thing that explains a startup failure — a stale
        // Swarnakshi.Api.exe holding bin/, a migration error, a bad connection string. Without it
        // the caller sees only "exited with code 1", which is useless. Kept to a bounded tail.
        var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Capture(string line)
        {
            output.Enqueue(line);
            while (output.Count > 40) output.TryDequeue(out _);
            log?.Invoke(line);
        }

        var process = ManagedProcess.Start(
            "dotnet",
            RepoPaths.Root,
            // --urls is passed as an APPLICATION argument (after --), not as ASPNETCORE_URLS.
            //
            // This matters and is easy to get wrong: appsettings.Development.json pins
            // "Urls": "http://localhost:6051", and WebApplication.CreateBuilder layers application
            // configuration OVER host configuration — so appsettings beats ASPNETCORE_URLS, and the
            // UAT API silently binds the DEVELOPER's port instead of its own. The command line is
            // the last provider registered, so it is the only one that reliably wins.
            // --no-build: the UAT project declares a build-order dependency on the API, so it is
            // already compiled by the time the fixture runs. Building here instead would contend
            // with any Swarnakshi.Api.exe still holding bin/ and fail as a bare exit code 1.
            //
            // The configuration must be the one that build produced, not a fixed "Debug". CI builds
            // the solution in Release only, so a hardcoded Debug looked for a binary that was never
            // produced and every scenario failed with "the API exited with code 1" — while passing
            // locally, where stale Debug output happened to be lying around.
            ["run", "--project", RepoPaths.ApiProject, "--no-launch-profile", "--no-build",
             "-c", BuildConfiguration, "--", "--urls", apiBase],
            new Dictionary<string, string>
            {
                // Development so the demo seed runs and the seeded owner exists to sign in as.
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                // Double underscore is the .NET convention for nesting: ConnectionStrings:Default.
                ["ConnectionStrings__Default"] = ConnectionFor(databaseName),
                ["Seed__Demo"] = "true",
                // The client is served from its own origin, so it must be allowed through CORS.
                ["Cors__Origins__0"] = options.BaseUrl.TrimEnd('/'),
            },
            Capture);

        // Generous: a cold `dotnet run` compiles first, then applies every migration to an empty
        // database and seeds the 50-category taxonomy before it answers.
        try
        {
            await ManagedProcess.WaitUntilRespondingAsync(
                $"{apiBase}/health", process, TimeSpan.FromMinutes(4), "The API", ct);
        }
        catch (InvalidOperationException ex)
        {
            // Exited before serving. Surface what it said on the way out.
            await ManagedProcess.StopAsync(process);
            throw new InvalidOperationException(
                $"{ex.Message}{Environment.NewLine}--- API output ---{Environment.NewLine}"
                + string.Join(Environment.NewLine, output), ex);
        }
        catch (TimeoutException ex)
        {
            // Overwhelmingly the cause is the API having bound a different port than asked for —
            // which is silent, and looks identical to a slow startup. Say so, because the symptom
            // gives no hint.
            await ManagedProcess.StopAsync(process);
            throw new TimeoutException(
                $"{ex.Message} The usual cause is the API binding a different port: check that " +
                "nothing in appsettings overrides the --urls argument this passes.", ex);
        }

        log?.Invoke($"API ready on {apiBase}");
        return new ApiServer(process, databaseName);
    }

    /// <summary>
    /// False once the API this run started has exited. A server that dies mid-run otherwise
    /// shows up as every remaining scenario timing out on connection-refused, which buries the one
    /// fact that matters under a dozen unrelated-looking failures.
    ///
    /// True when the run attached to a server it does not own — there is no process to watch.
    /// </summary>
    public bool IsAlive => _ownedProcess is null || !_ownedProcess.HasExited;

    public async ValueTask DisposeAsync()
    {
        await ManagedProcess.StopAsync(_ownedProcess);

        if (_databaseName is null) return;

        // WITH (FORCE): the API's connection pool can outlive the process by a moment, and DROP
        // fails outright while any session is still attached.
        try
        {
            await using var connection = new Npgsql.NpgsqlConnection(ConnectionFor("postgres"));
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);";
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // A leftover UAT database costs a few megabytes and is named for the run that made it,
            // so it is identifiable and disposable. Not worth failing a passing run over.
        }
    }
}
