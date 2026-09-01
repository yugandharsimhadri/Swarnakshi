using System.Diagnostics;
using Microsoft.Playwright;

namespace Swarnakshi.Automation;

/// <summary>
/// One narration beat and when it appeared, measured from the journey's title card.
///
/// The timing is what makes the transcript usable as subtitles or chapter markers: a list of
/// sentences says what happened, but not when to put it on screen.
/// </summary>
public sealed record NarrationBeat(string Text, TimeSpan At, bool IsTitle, int EstimatedSpeechMs);

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
    private readonly List<NarrationBeat> _beats = [];

    // Started on the first beat, which is the title card, so cue times are relative to the start of
    // the journey rather than to whenever the browser happened to launch.
    private readonly Stopwatch _clock = new();

    /// <summary>Every narration line spoken so far, in order.</summary>
    public IReadOnlyList<string> Steps => _beats.Select(b => b.Text).ToList();

    /// <summary>The same lines with the moment each appeared, for a transcript.</summary>
    public IReadOnlyList<NarrationBeat> Beats => _beats;

    /// <summary>How long the narration has been running, used to close the final cue.</summary>
    public TimeSpan Elapsed => _clock.Elapsed;

    private void Record(string text, bool isTitle)
    {
        if (!_clock.IsRunning) _clock.Start();
        _beats.Add(new NarrationBeat(text, _clock.Elapsed, isTitle, options.EstimatedSpeechMsFor(text)));
    }

    /// <summary>Announces the workflow. A title card when recording; a log line under test.</summary>
    public async Task AnnounceWorkflowAsync(string displayName, string module)
    {
        Record($"[{module}] {displayName}", isTitle: true);
        if (!options.ShowCaptions) return;

        var title = $"{module} — {displayName}";
        await ShowCaptionAsync(title);
        await Task.Delay(options.CaptionHoldMsFor(title));
    }

    /// <summary>Records a narration beat, showing it on screen before the step it describes runs.</summary>
    public async Task SayAsync(string narration)
    {
        Record(narration, isTitle: false);
        if (!options.ShowCaptions) return;

        await ShowCaptionAsync(narration);
        await Task.Delay(options.CaptionHoldMsFor(narration));
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
