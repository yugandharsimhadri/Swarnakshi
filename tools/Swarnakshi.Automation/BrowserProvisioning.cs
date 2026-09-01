using System.Diagnostics;
using Microsoft.Playwright;

namespace Swarnakshi.Automation;

/// <summary>
/// Launches Chromium, installing the build Playwright expects only if it turns out to be missing.
///
/// The install is deliberately not run up front on every session: 'playwright install' prunes
/// browser builds no longer referenced by the installed version, so calling it routinely would keep
/// evicting the builds the other Playwright suites on this machine depend on. Installing only after
/// an actual launch failure keeps the shared browser cache stable.
/// </summary>
public static class BrowserProvisioning
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _installAttempted;

    public static async Task<IBrowser> LaunchChromiumAsync(
        IPlaywright playwright,
        BrowserTypeLaunchOptions launchOptions,
        Action<string>? log = null)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(launchOptions);
        }
        catch (PlaywrightException ex) when (IsMissingBrowser(ex))
        {
            log?.Invoke("The Chromium build Playwright expects is not installed. Installing it now (one time).");
            await InstallChromiumAsync(log);
            return await playwright.Chromium.LaunchAsync(launchOptions);
        }
    }

    private static bool IsMissingBrowser(PlaywrightException ex)
        => ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Runs the install out of process with a timeout, rather than calling Playwright's Program.Main
    /// in-proc: a stalled download would otherwise wedge the test host with no way to observe it.
    /// </summary>
    private static async Task InstallChromiumAsync(Action<string>? log)
    {
        await InstallLock.WaitAsync();
        try
        {
            if (_installAttempted)
                throw new InvalidOperationException(
                    "Chromium is still unavailable after an install attempt in this run. " +
                    "Install it manually: pwsh bin/Debug/net10.0/playwright.ps1 install chromium");

            _installAttempted = true;

            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            if (exitCode != 0)
                throw new InvalidOperationException(
                    $"'playwright install chromium' exited with {exitCode}.");

            log?.Invoke("Chromium installed.");
        }
        finally
        {
            InstallLock.Release();
        }
    }
}
