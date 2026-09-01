using System.Text;
using Swarnakshi.Automation;
using Swarnakshi.Automation.Workflows;
using Xunit.Abstractions;

namespace Swarnakshi.UatTests;

/// <summary>
/// Base for the acceptance classes. Each test gets its own browser session, runs one workflow from
/// <see cref="WorkflowCatalog"/> in one viewport, and passes only if every verification inside that
/// workflow held.
///
/// The scenarios live in Swarnakshi.Automation rather than here on purpose: the same objects can be
/// replayed headed with captions to produce a walkthrough, so what is demonstrated and what is
/// signed off are the same journey by construction rather than by someone keeping two scripts in step.
/// </summary>
[Collection(UatCollection.Name)]
public abstract class UatTestBase(UatFixture fixture, ITestOutputHelper output)
{
    /// <summary>
    /// Every workflow is asserted in both viewports, and this is the data behind that
    /// <c>[Theory]</c>. A theory rather than two classes: Swarnakshi renders a desktop table and
    /// mobile cards from the same screen, and a class per viewport would invent modules like
    /// "Material Master Desktop" that no one would recognise on a UAT report. One class per module,
    /// two cases per test, and the viewport shows up in the case name where it belongs.
    /// </summary>
    public static TheoryData<Viewport> BothViewports => new() { Viewport.Desktop, Viewport.Mobile };

    /// <summary>Desktop only — for journeys whose assertions are about the desktop table itself.</summary>
    public static TheoryData<Viewport> DesktopOnly => new() { Viewport.Desktop };

    /// <summary>
    /// Runs the named workflow end to end in one viewport and asserts it completed. On failure the
    /// narration steps that did run are attached to the message, so the report names the business
    /// step that broke rather than just a locator.
    /// </summary>
    protected async Task RunWorkflowAsync(string workflowKey, Viewport viewport)
    {
        var workflow = WorkflowCatalog.Find(workflowKey)
            ?? throw new InvalidOperationException(
                $"No workflow named '{workflowKey}' in the catalog. Known keys: {WorkflowCatalog.KeyList}");

        var options = fixture.Options with { Viewport = viewport };

        await using var session = await SwarnakshiSession.StartAsync(options, output.WriteLine);
        await session.LoginAsync();

        var result = await WorkflowRunner.RunAsync(session, workflow, output.WriteLine);

        Assert.True(result.Succeeded, BuildFailureMessage(workflow, result));
    }

    private static string BuildFailureMessage(IWorkflow workflow, WorkflowRunResult result)
    {
        var message = new StringBuilder()
            .AppendLine($"UAT scenario '{workflow.DisplayName}' ({workflow.Key}) failed on {result.Viewport}.")
            .AppendLine($"Purpose: {workflow.BusinessPurpose}")
            .AppendLine()
            .AppendLine($"Failed after {result.NarrationSteps.Count} step(s):");

        foreach (var (step, index) in result.NarrationSteps.Select((s, i) => (s, i + 1)))
            message.AppendLine($"  {index,2}. {step}");

        message.AppendLine().AppendLine($"Reason: {result.FailureMessage}");

        if (result.ScreenshotPath is not null)
            message.AppendLine($"Screenshot: {result.ScreenshotPath}");

        return message.ToString();
    }
}
