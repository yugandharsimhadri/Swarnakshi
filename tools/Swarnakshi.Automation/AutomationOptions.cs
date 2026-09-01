namespace Swarnakshi.Automation;

/// <summary>
/// How a scenario is driven. The steps are identical either way — only pacing and captions differ,
/// so a recorded walkthrough and a UAT run are the same journey through the product.
/// </summary>
public enum RunMode
{
    /// <summary>Headless, no pacing, no captions — as fast as the browser will go.</summary>
    Test,

    /// <summary>Headed, moderate pacing, one caption per narration beat.</summary>
    Demo,
}

/// <summary>
/// Which shape of screen the journey is driven on. Swarnakshi is used on a phone at the site as much
/// as at a desk, and the two are genuinely different to operate: the masters render a table on
/// desktop and cards on mobile, and the bottom tab bar carries only five destinations with the rest
/// behind "More". A run covers one viewport; the UAT covers both.
/// </summary>
public enum Viewport
{
    /// <summary>A desktop browser wide enough for the <c>lg:</c> breakpoint, where the tables appear.</summary>
    Desktop,

    /// <summary>
    /// A phone, via Playwright's own device descriptor — so user agent, scale factor, touch support
    /// and viewport move together. Resizing the window alone would leave the app believing it is a
    /// desktop in a narrow window, and pointer-coarse queries would not engage.
    /// </summary>
    Mobile,
}

/// <summary>
/// Every knob the automation reads, resolved from environment variables so CI can steer a run
/// without a rebuild. All values have working defaults, so a bare <c>dotnet test</c> does the right
/// thing with no configuration.
/// </summary>
public sealed record AutomationOptions
{
    /// <summary>
    /// Where the Vite client is served for a UAT run. Deliberately NOT the 6050 a developer uses:
    /// a run must never attach to, or disturb, the dev server someone has open. Vite is started with
    /// strictPort, so a clash fails loudly instead of silently hopping to another port.
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:6070";

    /// <summary>
    /// Where the API is served, and what the client's <c>/api</c> proxy is pointed at. 6071 for the
    /// same reason as 6070 — the developer's 6051 is left strictly alone.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "http://localhost:6071";

    public RunMode RunMode { get; init; } = RunMode.Test;

    public Viewport Viewport { get; init; } = Viewport.Desktop;

    /// <summary>Absolute path to web/, used to start the client on demand.</summary>
    public string WebProjectPath { get; init; } = "";

    /// <summary>
    /// When false the automation assumes something else already serves both URLs and will neither
    /// start nor stop them — useful while writing a workflow against servers you control.
    /// </summary>
    public bool ManageServers { get; init; } = true;

    /// <summary>
    /// The phone the mobile viewport emulates, by Playwright device-descriptor name. Overridable
    /// because that list shifts between Playwright versions, and a name that disappears should be a
    /// setting to change rather than a rebuild.
    /// </summary>
    public string MobileDevice { get; init; } = "iPhone 15 Pro";

    /// <summary>
    /// Desktop page size. 1440x900 rather than 1920x1080 because what actually matters here is
    /// clearing Tailwind's <c>lg</c> breakpoint (1024px) where the master tables replace the mobile
    /// cards — this is comfortably past it while still fitting a laptop screen in a headed run.
    /// </summary>
    public int DesktopWidth { get; init; } = 1440;

    public int DesktopHeight { get; init; } = 900;

    /// <summary>Per-action delay Playwright applies. Raised in Demo so the cursor is followable.</summary>
    public int SlowMoMs => RunMode == RunMode.Demo ? 220 : 0;

    /// <summary>How long a caption stays on screen before the step it describes runs.</summary>
    public int CaptionHoldMs => RunMode == RunMode.Demo ? 1500 : 0;

    /// <summary>
    /// Whether the browser is visible. Headed by default: this suite is the walkthrough of the
    /// product as much as its test, and a run nobody can see is one nobody can film or trust.
    ///
    /// Forced off on CI, where there is no display — a headed Chromium on a bare runner fails to
    /// launch rather than quietly falling back. SWARNAKSHI_UAT_HEADED overrides either way.
    ///
    /// Headed is only visibility. Pacing and captions belong to Demo mode, so an ordinary headed run
    /// is still as fast as the browser will go.
    /// </summary>
    public bool Headed { get; init; } = true;

    public bool ShowCaptions => RunMode != RunMode.Test;

    /// <summary>
    /// Builds the options from environment variables, falling back to the defaults above:
    /// SWARNAKSHI_UAT_BASE_URL, SWARNAKSHI_UAT_API_BASE_URL, SWARNAKSHI_UAT_RUN_MODE (test|demo),
    /// SWARNAKSHI_UAT_VIEWPORT (desktop|mobile), SWARNAKSHI_UAT_WEB_PATH,
    /// SWARNAKSHI_UAT_MANAGE_SERVERS (true|false), SWARNAKSHI_UAT_MOBILE_DEVICE,
    /// SWARNAKSHI_UAT_DESKTOP_SIZE (WIDTHxHEIGHT), SWARNAKSHI_UAT_HEADED (true|false).
    /// </summary>
    public static AutomationOptions FromEnvironment(
        RunMode defaultRunMode = RunMode.Test,
        Viewport defaultViewport = Viewport.Desktop)
    {
        var (width, height) = ParseSize(Env("SWARNAKSHI_UAT_DESKTOP_SIZE"), 1440, 900);

        return new AutomationOptions
        {
            BaseUrl = Env("SWARNAKSHI_UAT_BASE_URL") ?? "http://localhost:6070",
            ApiBaseUrl = Env("SWARNAKSHI_UAT_API_BASE_URL") ?? "http://localhost:6071",
            RunMode = ParseEnum(Env("SWARNAKSHI_UAT_RUN_MODE"), defaultRunMode),
            Viewport = ParseEnum(Env("SWARNAKSHI_UAT_VIEWPORT"), defaultViewport),
            WebProjectPath = Env("SWARNAKSHI_UAT_WEB_PATH") ?? RepoPaths.WebProject,
            ManageServers = !string.Equals(Env("SWARNAKSHI_UAT_MANAGE_SERVERS"), "false",
                StringComparison.OrdinalIgnoreCase),
            MobileDevice = Env("SWARNAKSHI_UAT_MOBILE_DEVICE") ?? "iPhone 15 Pro",
            Headed = ParseBool(Env("SWARNAKSHI_UAT_HEADED")) ?? !IsContinuousIntegration,
            DesktopWidth = width,
            DesktopHeight = height,
        };
    }

    /// <summary>
    /// Parses WIDTHxHEIGHT. Rejected rather than quietly defaulted: someone who typed "1440*900"
    /// needs telling, not handing a silently different window than they asked for.
    /// </summary>
    private static (int Width, int Height) ParseSize(string? value, int defaultWidth, int defaultHeight)
    {
        if (value is null) return (defaultWidth, defaultHeight);

        var parts = value.Split('x', 'X');
        if (parts.Length == 2
            && int.TryParse(parts[0].Trim(), out var w)
            && int.TryParse(parts[1].Trim(), out var h)
            && w > 0 && h > 0)
        {
            return (w, h);
        }

        throw new ArgumentException(
            $"SWARNAKSHI_UAT_DESKTOP_SIZE is '{value}', which is not a size. Use WIDTHxHEIGHT, e.g. 1440x900.");
    }

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Both are set by GitHub Actions; either is enough to mean "no display here".</summary>
    private static bool IsContinuousIntegration
        => Env("CI") is not null || Env("GITHUB_ACTIONS") is not null;

    private static bool? ParseBool(string? value)
        => bool.TryParse(value, out var parsed) ? parsed : null;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
