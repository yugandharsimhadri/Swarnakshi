using Swarnakshi.Automation;
using Xunit.Abstractions;

namespace Swarnakshi.UatTests;

/// <summary>
/// The journey the product exists for. Material is bought into a site, and that purchase is
/// inventory value — not yet any project's cost. This is the half of the invariant that the site
/// office performs at the gate.
/// </summary>
public sealed class PurchaseUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Buying_material_into_a_site(Viewport viewport) => RunWorkflowAsync("PurchaseToConsumption", viewport);
}

/// <summary>
/// Releasing site stock to a project — request, owner approval, then issue. Three separate acts on
/// purpose, because this is the point at which material stops being inventory and becomes a
/// project's cost, and a site cannot release its own stock.
/// </summary>
public sealed class MaterialRequestUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Requesting_approving_and_issuing_stock(Viewport viewport) => RunWorkflowAsync("MaterialRequestApproval", viewport);
}

/// <summary>
/// What each site is holding and what it is worth. Inventory is site-level — one pool the projects
/// on that site draw from — which is why the screen is scoped to a site rather than to a project.
/// </summary>
public sealed class SiteInventoryUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task What_the_site_is_holding(Viewport viewport) => RunWorkflowAsync("SiteInventory", viewport);
}

/// <summary>
/// The standing reports, derived from the same records the screens show — there is no second set of
/// books to reconcile against.
/// </summary>
public sealed class ReportsUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task The_standing_reports(Viewport viewport) => RunWorkflowAsync("Reports", viewport);
}
