using Microsoft.Playwright;

namespace Swarnakshi.Automation.Workflows;

/// <summary>
/// The material catalogue a construction business actually buys from. What makes this master
/// non-trivial is that an exact purchasable material is Name + Company/Brand + specifications —
/// Polycab 2.5 sq.mm wire is a different stock item from Finolex 2.5 sq.mm, and from Polycab 4
/// sq.mm — and the specification fields on the form change with the subcategory chosen.
/// </summary>
public sealed class MaterialCatalogueWorkflow() : Workflow(
    key: "MaterialCatalogue",
    displayName: "Finding A Material",
    module: "Material Master",
    businessPurpose: "Find one exact material among a catalogue of them — by code, by company, or by "
        + "specification — so the right stock item is bought and issued rather than an approximation.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        await c.StepAsync(
            "Material Master opens from the Stock hub.",
            () => c.OpenFromStockHubAsync("Material Master", "Material Master"));

        await c.StepAsync(
            "It opens on the size of the catalogue: how many materials, how many active, and how "
                + "many categories they are filed under.",
            async () =>
            {
                await c.ExpectVisibleAsync("Total Materials");
                await c.ExpectVisibleAsync("Categories");
            });

        await c.StepAsync(
            "Searching by name finds the cement.",
            async () =>
            {
                await c.SearchAsync("Search code, name, company, category or specification…", "cement");
                await c.ExpectVisibleAsync(DemoData.CementName);
            });

        await c.StepAsync(
            "Search reaches specification values too, not just names — asking for 2.5 finds the wire "
                + "by its size, which is the way a site engineer actually asks for it.",
            async () =>
            {
                await c.SearchAsync("Search code, name, company, category or specification…", "2.5");
                await c.ExpectVisibleAsync(DemoData.WireSpecSummary);
            });

        await c.StepAsync(
            "Clearing the filters returns the whole catalogue.",
            async () =>
            {
                await c.Button("Clear Filters").ClickAsync();
                await c.ExpectVisibleAsync("Total Materials");
            });
    }
}

/// <summary>
/// Adding an exact material to the catalogue. The specification fields are decided by the
/// subcategory — choosing Electrical Wire asks for Size and Size Unit and nothing else — which is
/// what keeps the master from becoming a wall of irrelevant nullable columns.
/// </summary>
public sealed class AddMaterialWorkflow() : Workflow(
    key: "AddMaterial",
    displayName: "Adding An Exact Material",
    module: "Material Master",
    businessPurpose: "Define one exact purchasable material — brand and specification included — so "
        + "stock, purchases and consumption all refer to the same thing.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        // Unique per run: the master refuses an exact duplicate of name + brand + specification, and
        // that refusal is a feature this suite must not trip over on a re-run.
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var code = $"UAT-WIR-{suffix}";
        var brand = $"Polycab {suffix}";

        await c.StepAsync(
            "Adding a material starts from the Material Master screen.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Material Master", "Material Master");
                await c.Button("+ Add Material").ClickAsync();
                await c.ExpectHeadingAsync("Add Material");
            });

        await c.StepAsync(
            "The identity comes first: the company's own code, the material, and who makes it.",
            async () =>
            {
                await c.FillAsync("Material Code *", code);
                await c.FillAsync("Material Name *", "Electrical Wire");
                await c.SelectAsync("Category *", DemoData.ElectricalWireCategory);
                await c.SelectAsync("Subcategory *", "Single Core");
                await c.FillAsync("Company / Brand", brand);
            });

        await c.StepAsync(
            "Choosing the subcategory decided which specifications apply — wire is asked for its size "
                + "and nothing irrelevant.",
            async () =>
            {
                await c.FillAsync("Size *", "2.5");
                await c.SelectAsync("Size Unit *", "sq.mm");
            });

        await c.StepAsync(
            "Measurement separates the stock unit from how the material is packed: bought by the "
                + "metre, delivered in coils of ninety.",
            async () =>
            {
                await c.SelectAsync("Primary Unit *", "Meter");
                await c.FillAsync("Generic Measurement", "90 Meter / Coil");
            });

        await c.StepAsync(
            "A default rate and GST are reference values — the real cost still comes from the purchase.",
            async () =>
            {
                await c.FillAsync("Default Purchase Rate", "55");
                await c.FillAsync("GST %", "18");
            });

        await c.StepAsync(
            "Saved, the material joins the catalogue with its specification summarised for the list.",
            async () =>
            {
                await c.Button("Create Material").ClickAsync();
                await WorkflowContext.Expect(c.Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
                await c.SearchAsync("Search code, name, company, category or specification…", code);
                await c.ExpectVisibleAsync(code);
                await c.ExpectVisibleAsync(brand);
            });
    }
}

/// <summary>
/// Retiring a material without losing its history. A material that has been bought and consumed can
/// never simply be deleted — the purchases and issues that reference it must keep resolving — so the
/// master deactivates instead, which removes it from new transactions and leaves the past intact.
/// </summary>
public sealed class MaterialLifecycleWorkflow() : Workflow(
    key: "MaterialLifecycle",
    displayName: "Retiring And Restoring A Material",
    module: "Material Master",
    businessPurpose: "Stop a material being used on new purchases without deleting it, so every "
        + "historical purchase, issue and project cost that referenced it still reads correctly.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var code = $"UAT-LIF-{suffix}";

        await c.StepAsync(
            "A material is added for the purpose.",
            async () =>
            {
                await c.OpenFromStockHubAsync("Material Master", "Material Master");
                await c.Button("+ Add Material").ClickAsync();
                // Wait for the sheet before typing into it. Without this the first fill can land
                // while the form is still mounting, and the value is lost when React re-renders —
                // the save then succeeds with an empty code and the record is unfindable later.
                await c.ExpectHeadingAsync("Add Material");
                await c.FillAsync("Material Code *", code);
                await c.FillAsync("Material Name *", $"Retirement Test {suffix}");
                await c.SelectAsync("Category *", DemoData.CementCategory);
                await c.SelectAsync("Subcategory *", "OPC");
                await c.SelectAsync("Primary Unit *", "Bag");

                // Assert the form actually holds what was typed before submitting. Without this a
                // value lost to a re-render produces a save that "succeeds" with different data, and
                // the scenario then fails several steps later looking for a record that was never
                // created under that code — pointing at the wrong screen entirely.
                await WorkflowContext.Expect(c.Field("Material Code *", "input")).ToHaveValueAsync(code);

                await c.Button("Create Material").ClickAsync();
                // Wait for the SHEET to close, not for the page heading and not for the submit
                // button. The heading stays visible behind the sheet, so asserting it passes even
                // when the save was rejected; and the button can flicker out of the DOM during a
                // re-render, which satisfies a hidden-check without the save having succeeded.
                // The dialog disappearing is the only signal that actually means "saved" — and if
                // it does not, the failure screenshot carries the validation error still on screen.
                await WorkflowContext.Expect(c.Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
            });

        await c.StepAsync(
            "Finding it again, it is Active.",
            async () =>
            {
                await c.SearchAsync("Search code, name, company, category or specification…", code);
                await c.ExpectVisibleAsync(code);
            });

        await c.StepAsync(
            "Deactivating asks for confirmation, and says plainly what it will and will not change.",
            async () =>
            {
                await c.RevealRowActionsAsync();
                await c.RowAction(code, "Deactivate").ClickAsync();
                await c.ExpectVisibleAsync("Deactivate material?");
                await c.ExpectVisibleAsync("Existing transaction history will remain unchanged.");
            });

        await c.StepAsync(
            "Confirmed, it leaves the active catalogue but is still there under Inactive.",
            async () =>
            {
                await c.ConfirmAsync("Deactivate");
                // The row is still listed (no status filter yet) — it now reads Inactive. Asserting
                // that here proves the deactivation took AND holds the scenario until the list has
                // caught up, before any filtering is involved.
                await c.ExpectRowStatusAsync(code, "Inactive");
                await c.SettleAsync();
                // Clear the search before switching status, so the list is filtered by ONE thing.
                // A debounced search term and a filter change settle independently and each fires
                // its own request; combining them here made the step depend on which landed last.
                // Status alone is also the honest question being asked: is it in the inactive list.
                await c.SearchAsync("Search code, name, company, category or specification…", "");
                await c.SettleAsync();
                await c.SelectFilterAsync("Any status", "Inactive");
                await c.SettleAsync();
                await c.ExpectVisibleAsync(code);
            });

        await c.StepAsync(
            "Reactivating puts it back into use, with its history untouched throughout.",
            async () =>
            {
                await c.RevealRowActionsAsync();
                await c.RowAction(code, "Reactivate").ClickAsync();
                await c.ConfirmAsync("Reactivate");
                await c.SettleAsync();
                await c.SelectFilterAsync("Any status", "Active");
                await c.SettleAsync();
                // Back among the active materials. Searched by code because the active list is the
                // whole 40-material catalogue, and only one filter is in play at a time.
                await c.SearchAsync("Search code, name, company, category or specification…", code);
                await c.SettleAsync();
                await c.ExpectVisibleAsync(code);
            });
    }
}

/// <summary>
/// The people the business buys labour from. A contractor carries the identity and tax details a
/// payment needs, and — like every master here — is retired by deactivation rather than deletion,
/// because contracts and payments must keep resolving whose work they were.
/// </summary>
public sealed class ContractorMasterWorkflow() : Workflow(
    key: "ContractorMaster",
    displayName: "Keeping The Contractor List",
    module: "Party Master",
    businessPurpose: "Hold each contractor's identity, tax and bank details once, so contracts and "
        + "payments reference a real record rather than a name typed differently each time.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var code = $"UAT-CON-{suffix}";
        var name = $"Suresh Electricals {suffix}";

        await c.StepAsync(
            "Contractors open from More.",
            async () =>
            {
                await c.NavigateAsync("Contractors", "Contractors");
                await c.ExpectVisibleAsync("Contractor master");
            });

        await c.StepAsync(
            "The list opens on active contractors only — the ones available for new work.",
            () => c.ExpectVisibleAsync("Active"));

        await c.StepAsync(
            "Adding one captures identity, contact and tax details in named sections.",
            async () =>
            {
                await c.Button("+ Add Contractor").ClickAsync();
                await c.ExpectHeadingAsync("Add Contractor");
                await c.FillAsync("Contractor Code *", code);
                await c.FillAsync("Name *", name);
                await c.FillAsync("Company Name", "Suresh & Co");
                await c.FillAsync("Contractor Type", "Electrical");
                await c.FillAsync("Mobile", "9876543210");
                await c.FillAsync("PAN", "ABCDE1234F");
                await c.Button("Create Contractor").ClickAsync();
            });

        await c.StepAsync(
            "The new contractor is on the list and available for contracts.",
            async () =>
            {
                await c.ExpectHeadingAsync("Contractors");
                await c.SearchAsync("Search contractors…", code);
                await c.ExpectVisibleAsync(name);
            });

        await c.StepAsync(
            "Deactivating explains that existing contracts and payments are left alone.",
            async () =>
            {
                await c.RevealRowActionsAsync();
                await c.RowAction(code, "Deactivate").ClickAsync();
                await c.ExpectVisibleAsync("Deactivate contractor?");
                await c.ExpectVisibleAsync("Existing historical records will remain unchanged.");
                await c.ConfirmAsync("Deactivate");
                // Prove the deactivation landed before filtering on it. The list still carries the
                // row (status filter is Active by default, but the refresh has not run yet), so this
                // also holds the scenario until the list reflects the change.
                await c.SettleAsync();
                await c.SearchAsync("Search contractors…", code);
                await c.SelectFilterAsync("All", "All");
                await c.SettleAsync();
                await c.ExpectRowStatusAsync(code, "Inactive");
            });

        await c.StepAsync(
            "It is gone from the active list but still findable under Inactive.",
            async () =>
            {
                await c.SelectFilterAsync("All", "Inactive");
                await c.SettleAsync();

                // Re-enter the search after switching status. Changing the filter re-queries with
                // whatever the debounced search term is at that moment, and the two settle
                // independently — so asserting on the box's leftover text races the request it
                // belongs to. Typing again is also simply what a user does.
                await c.SearchAsync("Search contractors…", code);
                await c.ExpectVisibleAsync(name);
            });
    }
}

/// <summary>
/// The people the business sells to. A customer is attached to projects and receipts, so the same
/// deactivate-never-delete rule applies for the same reason.
/// </summary>
public sealed class CustomerMasterWorkflow() : Workflow(
    key: "CustomerMaster",
    displayName: "Keeping The Customer List",
    module: "Party Master",
    businessPurpose: "Hold each customer once, so projects and receipts point at a real record and "
        + "the outstanding figure is per customer rather than per spelling of their name.")
{
    public override async Task RunAsync(WorkflowContext c)
    {
        var suffix = DateTime.Now.ToString("HHmmssfff");
        var code = $"UAT-CUST-{suffix}";
        var name = $"Ramesh Kumar {suffix}";

        await c.StepAsync(
            "Customers open from More.",
            async () =>
            {
                await c.NavigateAsync("Customers", "Customers");
                await c.ExpectVisibleAsync("Customer master");
            });

        await c.StepAsync(
            "Adding a customer captures identity, contact and GSTIN.",
            async () =>
            {
                await c.Button("+ Add Customer").ClickAsync();
                await c.ExpectHeadingAsync("Add Customer");
                await c.FillAsync("Customer Code *", code);
                await c.FillAsync("Name *", name);
                await c.FillAsync("Mobile", "9000000123");
                await c.FillAsync("GSTIN", "36ABCDE1234F1Z5");
                await c.Button("Create Customer").ClickAsync();
            });

        await c.StepAsync(
            "The customer is on the list, ready to be attached to a project.",
            async () =>
            {
                await c.ExpectHeadingAsync("Customers");
                await c.SearchAsync("Search customers…", code);
                await c.ExpectVisibleAsync(name);
            });

        await c.StepAsync(
            "Opening the record shows what already references it — projects and receipts — which is "
                + "why it is retired rather than deleted.",
            async () =>
            {
                await c.RevealRowActionsAsync();
                await c.OpenDetailAsync(name);
                await c.ExpectVisibleAsync("Usage");
                await c.ExpectVisibleAsync("Projects");
            });
    }
}
