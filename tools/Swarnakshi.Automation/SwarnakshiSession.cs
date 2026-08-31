using Microsoft.Playwright;
using Swarnakshi.Automation.Workflows;

namespace Swarnakshi.Automation;

/// <summary>
/// One browser session against the Swarnakshi client: owns Playwright, the browser, the page and the
/// narrator, and knows how to sign in. Both the UAT suite and any recorded walkthrough build their
/// <see cref="WorkflowContext"/> from an instance of this, which is what keeps the two the same
/// journey through the app.
/// </summary>
public sealed class SwarnakshiSession : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly IBrowserContext _context;

    private SwarnakshiSession(
        IPlaywright playwright, IBrowser browser, IBrowserContext context,
        IPage page, Narrator narrator, AutomationOptions options)
    {
        _playwright = playwright;
        _browser = browser;
        _context = context;
        Page = page;
        Narrator = narrator;
        Options = options;
    }

    public IPage Page { get; }

    public Narrator Narrator { get; }

    public AutomationOptions Options { get; }

    /// <summary>
    /// Launches the browser in the viewport the options ask for. The servers are expected to be up
    /// already — the fixture starts them once for the whole run, not once per session.
    /// </summary>
    public static async Task<SwarnakshiSession> StartAsync(AutomationOptions options, Action<string>? log = null)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();

            browser = await BrowserProvisioning.LaunchChromiumAsync(
                playwright,
                new BrowserTypeLaunchOptions { Headless = !options.Headed, SlowMo = options.SlowMoMs },
                log);

            var contextOptions = BuildContextOptions(playwright, options);
            contextOptions.BaseURL = options.BaseUrl;
            contextOptions.Locale = "en-IN";
            // Money is formatted en-IN and dates are rendered in the viewer's zone; pinning both
            // keeps "₹80.00L" and "31 Aug 2026" the same strings on any machine that runs this.
            contextOptions.TimezoneId = "Asia/Kolkata";

            var context = await browser.NewContextAsync(contextOptions);
            var page = await context.NewPageAsync();

            // Generous but finite. The client is a dev server, so the first navigation of a run
            // waits on Vite's on-demand transform of the whole module graph.
            page.SetDefaultTimeout(20_000);
            page.SetDefaultNavigationTimeout(60_000);

            // Assertions carry their OWN timeout, which defaults to 5s and is NOT affected by
            // SetDefaultTimeout above. That default is too tight here: the dashboard heading is
            // "Hi, {name}" and renders empty until /auth/me resolves, so a correct app fails a
            // correct assertion purely on timing. Raised once, globally, rather than passed at
            // every call site.
            Assertions.SetDefaultExpectTimeout(15_000);

            return new SwarnakshiSession(playwright, browser, context, page,
                new Narrator(page, options), options);
        }
        catch
        {
            if (browser is not null) await browser.CloseAsync();
            playwright?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Mobile goes through Playwright's device descriptor so user agent, scale factor and touch
    /// support move together with the viewport — the app's <c>lg:</c> breakpoint decides between the
    /// master table and the mobile cards, and a desktop UA in a narrow window is not the same test.
    /// </summary>
    private static BrowserNewContextOptions BuildContextOptions(IPlaywright playwright, AutomationOptions options)
    {
        if (options.Viewport == Viewport.Mobile)
        {
            if (!playwright.Devices.TryGetValue(options.MobileDevice, out var device))
                throw new InvalidOperationException(
                    $"Playwright has no device descriptor named '{options.MobileDevice}'. " +
                    "Set SWARNAKSHI_UAT_MOBILE_DEVICE to one it does have.");

            return new BrowserNewContextOptions
            {
                UserAgent = device.UserAgent,
                ViewportSize = device.ViewportSize,
                DeviceScaleFactor = device.DeviceScaleFactor,
                IsMobile = device.IsMobile,
                HasTouch = device.HasTouch,
            };
        }

        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = options.DesktopWidth, Height = options.DesktopHeight },
        };
    }

    /// <summary>
    /// Signs in as the seeded owner and waits for the app shell.
    ///
    /// Arrival is asserted on the bottom navigation rather than on a dashboard figure: login is a
    /// client-side route change with no load event, and the KPI values depend on what the run has
    /// done so far, whereas the shell is the same on every screen.
    /// </summary>
    public async Task LoginAsync(string? email = null, string? password = null)
    {
        await Page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Page.GetByLabel("Email").FillAsync(email ?? DemoData.OwnerEmail);
        await Page.GetByLabel("Password").FillAsync(password ?? DemoData.OwnerPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Navigation).First)
            .ToBeVisibleAsync(new() { Timeout = 60_000 });
    }

    public WorkflowContext CreateWorkflowContext() => new(Page, Narrator, Options);

    /// <summary>Writes a screenshot into artifacts/uat. Used on failure, and to close a recording.</summary>
    public async Task<string> CaptureScreenshotAsync(string name)
    {
        Directory.CreateDirectory(RepoPaths.ArtifactsDir);
        var safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(RepoPaths.ArtifactsDir,
            $"{safe}-{Options.Viewport}-{DateTime.Now:yyyyMMdd-HHmmss}.png");

        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _context.CloseAsync(); } catch { }
        try { await _browser.CloseAsync(); } catch { }
        _playwright.Dispose();
    }
}
