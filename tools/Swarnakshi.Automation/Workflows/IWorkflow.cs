namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// One end-to-end business journey through Swarnakshi, written once and consumed twice: the UAT
/// suite runs it headless and asserts it completes, and a Demo run replays it headed with captions.
/// The verifications live inside the workflow rather than in the tests, so a recorded walkthrough is
/// only ever produced from a journey that passed its own checks.
/// </summary>
public interface IWorkflow
{
    /// <summary>Stable token used to select the workflow. Treat it as an external contract.</summary>
    string Key { get; }

    /// <summary>Human-readable name, used for the title card and the UAT report.</summary>
    string DisplayName { get; }

    /// <summary>Which part of the product this belongs to.</summary>
    string Module { get; }

    /// <summary>One sentence on what this accomplishes for the construction business.</summary>
    string BusinessPurpose { get; }

    /// <summary>
    /// Runs the journey. The caller has already signed in; a workflow must assume no other prior
    /// state, so any subset can run in any order and in either viewport.
    /// </summary>
    Task RunAsync(WorkflowContext context);
}
