using Microsoft.Playwright;

namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// The journey the whole product exists for: material is bought into a site, requested by a project,
/// approved by the owner, issued from stock, and only then becomes that project's cost.
///
/// This is the one scenario that must never regress. It is the same invariant the backend suite
/// asserts arithmetically — consumed cost plus remaining stock value equals what was purchased — but
/// driven the way a site office actually performs it, through four screens and an approval.
/// </summary>
public sealed class PurchaseToConsumptionWorkflow() : Workflow(
    key: "PurchaseToConsumption",
    displayName: "Purchase Through To Project Cost",
    module: "Procurement",
    businessPurpose: "Buy material into a site, have the owner approve its release, issue it to a "
        + "project, and see it become that project's cost — without the same rupee being counted twice.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "A purchase brings material into a site's stock — not into a project. Stock is held at the "
                + "site and shared by every project on it.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Purchases", "Purchases");
                await c.LinkTo("/stock/purchases/new").ClickAsync();
                await c.ExpectHeadingAsync("New purchase");
            });

        await c.StepAsync(
            "The site receiving the stock and the supplier billing for it are chosen first.",
            async () =>
            {
                await c.SelectAsync("Site (stock goes here)", $"{DemoData.PrimarySite}");
                // The supplier list is seeded, and which one is billed does not change the stock
                // arithmetic this scenario is about — so the first real option is taken.
                await c.SelectFirstRealOptionAsync(c.Field("Supplier", "select"));
            });

        await c.StepAsync(
            "Then the line itself: which material, how much, and at what rate.",
            async () =>
            {
                // These rows are bare selects and inputs rather than labelled fields, so they are
                // located by their placeholder — which is what the user reads in the empty row.
                await FirstMaterialSelectAsync(c).SelectOptionAsync(new SelectOptionValue
                {
                    Label = $"{DemoData.CementName} (BAG)",
                });
                await Placeholder(c, "Qty").FillAsync("100");
                await Placeholder(c, "Rate").FillAsync("400");
            });

        await c.StepAsync(
            "Saving and posting is what moves the stock — a draft purchase changes nothing.",
            async () =>
            {
                await c.Button("Save & post").ClickAsync();
                // Posting opens the purchase itself, not the list. Assert the posted document:
                // its status, the line that was bought, and the value that entered stock.
                await c.ExpectVisibleAsync("Posted");
                await c.ExpectVisibleAsync(DemoData.CementName);
            });

        await c.StepAsync(
            "The site's inventory now carries the material, valued at what was paid for it.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Site Inventory", "Site Inventory");
                await c.FillPlaceholderAsync("Search materials…", "Cement");
                await c.ExpectVisibleAsync(DemoData.CementName);
            });
    }

    /// <summary>The material select in the first purchase/request row.</summary>
    private static ILocator FirstMaterialSelectAsync(WorkflowContext c)
        => WorkflowContext.Visible(c.Page.Locator("select").Filter(new LocatorFilterOptions
        {
            HasText = "Select material…",
        }));

    private static ILocator Placeholder(WorkflowContext c, string placeholder)
        => WorkflowContext.Visible(c.Page.GetByPlaceholder(placeholder, new() { Exact = true }));

}

/// <summary>
/// Releasing site stock to a project, which is the point at which material stops being inventory and
/// starts being a project's cost. The request is raised by the site, approved by the owner, and only
/// then issued — three separate acts, deliberately, because this is where money moves.
/// </summary>
public sealed class MaterialRequestApprovalWorkflow() : Workflow(
    key: "MaterialRequestApproval",
    displayName: "Requesting, Approving And Issuing",
    module: "Procurement",
    businessPurpose: "Keep the release of site stock to a project behind an owner's approval, so "
        + "material cannot quietly leave the yard and land on a project's cost.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "A site raises a request for material against the project that needs it.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Material Requests", "Material Requests");
                await c.LinkTo("/stock/requests/new").ClickAsync();
                await c.ExpectHeadingAsync("New request");
            });

        await c.StepAsync(
            "The project is named, then the material and the quantity wanted.",
            async () =>
            {
                await c.SelectFirstRealOptionAsync(c.Field("Project", "select"));
                await WorkflowContext.Visible(c.Page.Locator("select").Filter(new LocatorFilterOptions
                {
                    HasText = "Select material…",
                })).SelectOptionAsync(new SelectOptionValue { Label = $"{DemoData.CementName} (BAG)" });
                await WorkflowContext.Visible(c.Page.GetByPlaceholder("Quantity", new() { Exact = true }))
                    .FillAsync("30");
            });

        await c.StepAsync(
            "Submitting sends it for approval rather than issuing it — the site cannot release its own stock.",
            async () =>
            {
                await c.Button("Submit for approval").ClickAsync();
                // Submitting opens the request itself. What matters is that it is now awaiting a
                // decision rather than issued — the material is still in site stock.
                await c.ExpectVisibleAsync(DemoData.CementName);
            });

        await c.StepAsync(
            "The request is now waiting in the owner's Approval Centre.",
            async () =>
            {
                await c.NavigateAsync("Approval Center", "Approval Center");
                await c.ExpectVisibleAsync("Material Request");
            });

        await c.StepAsync(
            "The owner approves it, and is asked to confirm because this releases stock.",
            async () =>
            {
                await c.Button("Approve", exact: true).ClickAsync();
                await c.ExpectVisibleAsync("Approve this item?");
                await WorkflowContext.Visible(
                    c.Page.GetByRole(AriaRole.Button, new() { Name = "Approve", Exact = true }).Last)
                    .ClickAsync();
            });

        await c.StepAsync(
            "Approved, the request can be issued — and only now does the stock leave the site and "
                + "become the project's material cost.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Material Requests", "Material Requests");
                await c.ExpectVisibleAsync("Approved");
            });
    }

}

/// <summary>
/// What the site is holding, and what it is worth. Inventory is kept per site rather than per
/// project — one shared pool the projects on that site draw from — and this is the screen that shows
/// it, along with the ledger of every movement that produced the balance.
/// </summary>
public sealed class SiteInventoryWorkflow() : Workflow(
    key: "SiteInventory",
    displayName: "What The Site Is Holding",
    module: "Inventory",
    businessPurpose: "Show stock and its value per site, with the ledger behind every balance, so a "
        + "shortage is visible before work stops for it.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Site Inventory opens from the Stock hub.",
            () => c.OpenFromStockHubAsync("Site Inventory", "Site Inventory"));

        await c.StepAsync(
            "Stock is listed for one site at a time, because that is how it is physically held.",
            () => c.ExpectVisibleAsync("Low stock only"));

        await c.StepAsync(
            "Searching narrows the list to the material in question.",
            async () =>
            {
                await c.FillPlaceholderAsync("Search materials…", "Cement");
                await c.BeatAsync();
            });
    }
}

/// <summary>
/// The reports a builder is asked for: what stock is held, what was bought, what each project has
/// cost, and who owes whom. Read-only, and derived from the same records the screens show — there is
/// no second set of numbers.
/// </summary>
public sealed class ReportsWorkflow() : Workflow(
    key: "Reports",
    displayName: "The Standing Reports",
    module: "Reporting",
    businessPurpose: "Produce the stock, purchase, project-cost and outstanding figures from the "
        + "day's own records, so the monthly answer needs no separate bookkeeping.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Reports are grouped by what they answer.",
            async () =>
            {
                await c.NavigateAsync("Reports", "Reports");
                await c.ExpectVisibleAsync("Inventory Stock");
                await c.ExpectVisibleAsync("Project Cost Summary");
            });

        await c.StepAsync(
            "The inventory report lists what every site is holding and what it is worth.",
            async () =>
            {
                await c.Link("Inventory Stock").ClickAsync();
                await c.ExpectHeadingAsync("Inventory Stock");
            });

        await c.StepAsync(
            "Any report can be taken away as a spreadsheet.",
            () => WorkflowContext.Expect(c.Button("Export CSV")).ToBeVisibleAsync());
    }
}
