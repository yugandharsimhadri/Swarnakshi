using System.Diagnostics;

namespace Swarnakshi.Automation;

/// <summary>
/// Brings up the Swarnakshi API for a UAT run, against a database created fresh for that run.
///
/// The throwaway database is the important part: the suite signs in, creates materials, posts
/// purchases and issues stock. Pointed at a developer's swarnakshi.db it would leave that data
/// behind, and its assertions would be at the mercy of whatever was already there.
/// </summary>
public sealed class ApiServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;
    private readonly string? _databasePath;

    private ApiServer(Process? ownedProcess, string? databasePath)
    {
        _ownedProcess = ownedProcess;
        _databasePath = databasePath;
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

        Directory.CreateDirectory(RepoPaths.ArtifactsDir);
        var databasePath = Path.Combine(RepoPaths.ArtifactsDir, $"uat-{Guid.NewGuid():N}.db");

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
                ["ConnectionStrings__Default"] = $"Data Source={databasePath}",
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
        return new ApiServer(process, databasePath);
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

        if (_databasePath is null) return;

        // SQLite leaves -wal/-shm beside the database; removing only the .db would leak the pair.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            try
            {
                var path = _databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // The file is still locked by a process that has not finished dying. Harmless: it
                // lands in artifacts/uat, which is disposable by definition.
            }
        }
    }
}
