using System.Diagnostics;

namespace Swarnakshi.Automation;

/// <summary>
/// Brings up the Vite client for a UAT run, pointed at the run's own API.
///
/// It is started here rather than by hand for one reason: the client's <c>/api</c> proxy has to
/// reach the run's throwaway API, not the 6051 a developer has open. That is what
/// <c>SWARNAKSHI_API_URL</c> does in vite.config.ts.
/// </summary>
public sealed class WebDevServer : IAsyncDisposable
{
    private readonly Process? _ownedProcess;

    private WebDevServer(Process? ownedProcess) => _ownedProcess = ownedProcess;

    public static async Task<WebDevServer> StartAsync(
        AutomationOptions options,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/');

        if (await ManagedProcess.IsRespondingAsync(baseUrl, ct))
        {
            if (!options.ManageServers)
            {
                log?.Invoke($"Reusing the client already serving {baseUrl}");
                return new WebDevServer(ownedProcess: null);
            }

            // Something answers, but "a Vite dev server" is not the same as "this run's client
            // wired to this run's API". Attaching to a developer's 6050 would have the suite
            // writing test data into their database and reading their state back as assertions.
            throw new InvalidOperationException(
                $"Something is already serving {baseUrl}. The UAT run needs its own client, wired to " +
                "its own API. Stop it, or set SWARNAKSHI_UAT_BASE_URL.");
        }

        if (!options.ManageServers)
            throw new InvalidOperationException(
                $"Nothing is serving {baseUrl} and SWARNAKSHI_UAT_MANAGE_SERVERS=false, so the " +
                "automation will not start one.");

        var webPath = options.WebProjectPath;
        if (!Directory.Exists(webPath))
            throw new DirectoryNotFoundException($"Client project not found at '{webPath}'.");

        var viteBin = Path.Combine(webPath, "node_modules", "vite", "bin", "vite.js");
        if (!File.Exists(viteBin))
            throw new FileNotFoundException(
                $"Vite is not installed at '{viteBin}'. Run 'npm install' in {webPath}.", viteBin);

        var port = new Uri(baseUrl).Port;
        ManagedProcess.EnsurePortAvailable(port, "client");

        log?.Invoke($"Starting the Vite client on {baseUrl}");

        // Vite's own bin through node, not `npm run dev`. On Windows npm goes cmd.exe -> npm.cmd ->
        // node, and killing the tree from cmd routinely leaves the node grandchild alive still
        // holding the port — which then fails the *next* run as a port clash. One process, one kill.
        var process = ManagedProcess.Start(
            "node",
            webPath,
            [viteBin, "--port", port.ToString(), "--strictPort"],
            new Dictionary<string, string>
            {
                ["SWARNAKSHI_WEB_PORT"] = port.ToString(),
                ["SWARNAKSHI_API_URL"] = options.ApiBaseUrl.TrimEnd('/'),
            },
            log);

        await ManagedProcess.WaitUntilRespondingAsync(
            baseUrl, process, TimeSpan.FromMinutes(2), "The Vite client", ct);

        log?.Invoke($"Client ready on {baseUrl}");
        return new WebDevServer(process);
    }

    public ValueTask DisposeAsync() => new(ManagedProcess.StopAsync(_ownedProcess));
}
