using System.Diagnostics;

namespace Swarnakshi.Automation.Workflows;

/// <summary>The outcome of one workflow, as both the UAT suite and a recording run need it.</summary>
public sealed record WorkflowRunResult(
    string Key,
    string DisplayName,
    Viewport Viewport,
    bool Succeeded,
    TimeSpan Duration,
    IReadOnlyList<string> NarrationSteps,
    string? FailureMessage,
    string? ScreenshotPath)
{
    /// <summary>Where the narration transcript was written, if it could be.</summary>
    public string? NarrationPath { get; init; }
}

/// <summary>
/// Runs a workflow inside a session with the ceremony both callers need: the title card, timing, a
/// screenshot, and a CAPTURED failure rather than a thrown one — so a fifteen-scenario recording
/// does not stop dead on the fifth.
/// </summary>
public static class WorkflowRunner
{
    public static async Task<WorkflowRunResult> RunAsync(
        SwarnakshiSession session,
        IWorkflow workflow,
        Action<string>? log = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = session.CreateWorkflowContext();

        await session.Narrator.AnnounceWorkflowAsync(workflow.DisplayName, workflow.Module);

        try
        {
            await workflow.RunAsync(context);
            await session.Narrator.CloseAsync();
            stopwatch.Stop();

            string? screenshot = null;
            if (session.Options.RunMode != RunMode.Test)
                screenshot = await session.CaptureScreenshotAsync($"{workflow.Key}-final");

            log?.Invoke($"PASS {workflow.Key} [{session.Options.Viewport}] ({stopwatch.Elapsed.TotalSeconds:0.0}s)");

            var passed = new WorkflowRunResult(workflow.Key, workflow.DisplayName, session.Options.Viewport,
                Succeeded: true, stopwatch.Elapsed, session.Narrator.Steps.ToList(), null, screenshot);

            return passed with { NarrationPath = WriteTranscript(session, workflow, passed) };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Best effort: if the page is what broke, the screenshot fails too, and the original
            // exception is the one worth reporting.
            string? screenshot = null;
            try { screenshot = await session.CaptureScreenshotAsync($"{workflow.Key}-FAILED"); } catch { }

            log?.Invoke($"FAIL {workflow.Key} [{session.Options.Viewport}] ({stopwatch.Elapsed.TotalSeconds:0.0}s): {ex.Message}");

            var failed = new WorkflowRunResult(workflow.Key, workflow.DisplayName, session.Options.Viewport,
                Succeeded: false, stopwatch.Elapsed, session.Narrator.Steps.ToList(), ex.Message, screenshot);

            // Written for a failure too: the transcript ends at the step that broke, which is a
            // clearer account of how far the journey got than the step list in the message.
            return failed with { NarrationPath = WriteTranscript(session, workflow, failed) };
        }
    }

    /// <summary>
    /// The narration, with timings, alongside the run. The captions only ever existed on screen and
    /// xUnit surfaces the step list only when a test fails, so a green run left nothing to caption a
    /// recording with.
    /// </summary>
    private static string? WriteTranscript(SwarnakshiSession session, IWorkflow workflow, WorkflowRunResult result)
        => NarrationSidecar.TryWrite(
            workflow, result, session.Options, session.Narrator.Beats, session.Narrator.Elapsed);
}
