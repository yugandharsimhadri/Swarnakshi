namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// Every UAT scenario in one place, in the order a newcomer would want to see the product: get in,
/// see the position, set up the masters, then run the operational flow, then read the reports.
///
/// The UAT classes name workflows by key rather than constructing them, so a scenario is defined
/// once and cannot drift between what is tested and what is demonstrated.
/// </summary>
public static class WorkflowCatalog
{
    public static IReadOnlyList<IWorkflow> All { get; } =
    [
        new SignInWorkflow(),
        new DashboardWorkflow(),
        new UserAccessWorkflow(),

        new MaterialCatalogueWorkflow(),
        new AddMaterialWorkflow(),
        new MaterialLifecycleWorkflow(),
        new ContractorMasterWorkflow(),
        new CustomerMasterWorkflow(),

        new PurchaseToConsumptionWorkflow(),
        new MaterialRequestApprovalWorkflow(),
        new SiteInventoryWorkflow(),

        new ReportsWorkflow(),
    ];

    /// <summary>The workflow with this key, or null. Keys are case-insensitive for command-line use.</summary>
    public static IWorkflow? Find(string key)
        => All.FirstOrDefault(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every key, for error messages that tell the reader what they could have typed.</summary>
    public static string KeyList => string.Join(", ", All.Select(w => w.Key));
}
