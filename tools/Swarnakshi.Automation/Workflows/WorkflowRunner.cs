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
    string? ScreenshotPath);

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

            return new WorkflowRunResult(workflow.Key, workflow.DisplayName, session.Options.Viewport,
                Succeeded: true, stopwatch.Elapsed, session.Narrator.Steps.ToList(), null, screenshot);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Best effort: if the page is what broke, the screenshot fails too, and the original
            // exception is the one worth reporting.
            string? screenshot = null;
            try { screenshot = await session.CaptureScreenshotAsync($"{workflow.Key}-FAILED"); } catch { }

            log?.Invoke($"FAIL {workflow.Key} [{session.Options.Viewport}] ({stopwatch.Elapsed.TotalSeconds:0.0}s): {ex.Message}");

            return new WorkflowRunResult(workflow.Key, workflow.DisplayName, session.Options.Viewport,
                Succeeded: false, stopwatch.Elapsed, session.Narrator.Steps.ToList(), ex.Message, screenshot);
        }
    }
}
