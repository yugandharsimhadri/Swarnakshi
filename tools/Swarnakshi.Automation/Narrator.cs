using Microsoft.Playwright;

namespace Swarnakshi.Automation;

/// <summary>
/// Records what a workflow is doing, in the business's own words.
///
/// Under test this is pure bookkeeping — the steps are collected so a failure can name the business
/// step that broke ("Posting the purchase moves it into site stock") rather than a CSS selector. In
/// Demo mode the same strings are drawn on screen as captions, which is what keeps a recorded
/// walkthrough and a UAT run the same journey rather than two scripts that drift apart.
/// </summary>
public sealed class Narrator(IPage page, AutomationOptions options)
{
    private readonly List<string> _steps = [];

    /// <summary>Every narration line spoken so far, in order.</summary>
    public IReadOnlyList<string> Steps => _steps;

    /// <summary>Announces the workflow. A title card when recording; a log line under test.</summary>
    public async Task AnnounceWorkflowAsync(string displayName, string module)
    {
        _steps.Add($"[{module}] {displayName}");
        if (!options.ShowCaptions) return;

        await ShowCaptionAsync($"{module} — {displayName}");
        await Task.Delay(options.CaptionHoldMs);
    }

    /// <summary>Records a narration beat, showing it on screen before the step it describes runs.</summary>
    public async Task SayAsync(string narration)
    {
        _steps.Add(narration);
        if (!options.ShowCaptions) return;

        await ShowCaptionAsync(narration);
        await Task.Delay(options.CaptionHoldMs);
    }

    /// <summary>A deliberate pause for the camera. Skipped under test, where waiting is wasted time.</summary>
    public Task BeatAsync(int milliseconds = 600)
        => options.ShowCaptions ? Task.Delay(milliseconds) : Task.CompletedTask;

    /// <summary>Clears the caption at the end of a workflow so the closing frame is clean.</summary>
    public async Task CloseAsync()
    {
        if (!options.ShowCaptions) return;
        await RemoveCaptionAsync();
    }

    /// <summary>
    /// Draws the caption into the page itself rather than compositing it later, so the recording
    /// needs no edit pass. Injected defensively: a caption failing must never fail the scenario,
    /// because the caption is not the thing under test.
    /// </summary>
    private async Task ShowCaptionAsync(string text)
    {
        try
        {
            await page.EvaluateAsync(
                """
                (text) => {
                  let el = document.getElementById('swk-uat-caption');
                  if (!el) {
                    el = document.createElement('div');
                    el.id = 'swk-uat-caption';
                    el.style.cssText = [
                      'position:fixed', 'left:50%', 'bottom:6%', 'transform:translateX(-50%)',
                      'max-width:80vw', 'padding:14px 22px', 'border-radius:14px',
                      'background:rgba(20,20,26,.92)', 'color:#fff', 'font:600 17px/1.45 system-ui,sans-serif',
                      'text-align:center', 'z-index:2147483647', 'pointer-events:none',
                      'box-shadow:0 8px 30px rgba(0,0,0,.35)'
                    ].join(';');
                    document.body.appendChild(el);
                  }
                  el.textContent = text;
                }
                """,
                text);
        }
        catch
        {
            // Mid-navigation the document is being replaced. The next caption lands fine.
        }
    }

    private async Task RemoveCaptionAsync()
    {
        try
        {
            await page.EvaluateAsync("() => document.getElementById('swk-uat-caption')?.remove()");
        }
        catch
        {
            // Same as above — nothing to clean up if the document went away.
        }
    }
}
