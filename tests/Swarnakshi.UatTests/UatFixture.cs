using Swarnakshi.Automation;

namespace Swarnakshi.UatTests;

/// <summary>
/// Shared setup for the whole UAT run: brings the API and the client up once, against a database
/// created for this run and deleted after it. Every test then opens its own browser session against
/// them, so no scenario inherits a browser another one left mid-journey.
///
/// Chromium is provisioned lazily on first launch rather than here — see BrowserProvisioning for why
/// installing it up front is harmful on a machine shared with other Playwright suites.
/// </summary>
public sealed class UatFixture : IAsyncLifetime
{
    private ApiServer? _api;
    private WebDevServer? _web;
    private StreamWriter? _apiLog;
    private StreamWriter? _webLog;

    public AutomationOptions Options { get; private set; } = AutomationOptions.FromEnvironment();

    public async Task InitializeAsync()
    {
        Options = AutomationOptions.FromEnvironment();

        // Both servers' output goes to a file, not just the console: xunit does not surface a
        // fixture's Console.WriteLine, so a server that dies mid-run leaves every later test failing
        // with a bare connection-refused and no explanation anywhere.
        Directory.CreateDirectory(RepoPaths.ArtifactsDir);
        _apiLog = OpenLog("api.log");
        _webLog = OpenLog("web.log");

        // API first: the client is started with its /api proxy pointed at it, and the seed has to be
        // in place before any test signs in.
        _api = await ApiServer.StartAsync(Options, line => Write(_apiLog, line));
        _web = await WebDevServer.StartAsync(Options, line => Write(_webLog, line));
    }

    public async Task DisposeAsync()
    {
        if (_web is not null) await _web.DisposeAsync();
        if (_api is not null) await _api.DisposeAsync();
        _webLog?.Dispose();
        _apiLog?.Dispose();
    }

    private static StreamWriter OpenLog(string name)
        => new(Path.Combine(RepoPaths.ArtifactsDir, name), append: false) { AutoFlush = true };

    /// <summary>Server output can arrive from several reader threads; serialise it.</summary>
    private static void Write(StreamWriter? log, string line)
    {
        if (log is null) return;
        lock (log) log.WriteLine(line);
        Console.WriteLine(line);
    }
}

/// <summary>
/// Puts every UAT class in one collection so they share the fixture and run one after another. They
/// drive real browsers against one client and one SQLite file; in parallel they would contend for
/// both, and two scenarios posting stock at once would make each other's assertions unpredictable.
/// </summary>
[CollectionDefinition(Name)]
public sealed class UatCollection : ICollectionFixture<UatFixture>
{
    public const string Name = "Swarnakshi UAT";
}
