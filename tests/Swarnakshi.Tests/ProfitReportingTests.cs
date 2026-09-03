using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Customers;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Reports;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// The two reports that carry a judgement rather than a sum.
///
/// Both exist because the app's plain arithmetic was misleading in the dangerous direction: a
/// half-built villa was reporting the whole sale value as profit, and estimate-minus-actual made an
/// unfinished house look like money saved. These pin the corrected arithmetic down.
/// </summary>
public class ProfitReportingTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid SiteId, Guid SupplierId, Material Cement, Guid CustomerId);

    private static async Task<Fixture> ArrangeAsync(IServiceProvider sp, AppDbContext db)
    {
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(
            new SaveSiteRequest(null, "Green Meadows", null, null, null, null, null, null, SiteStatus.Active, null));

        var supplier = new Supplier { Code = "SUP-1", Name = "Sri Balaji Traders" };
        db.Suppliers.Add(supplier);
        var customer = new Customer { Code = "CUS-1", Name = "Prasad Rao" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var cement = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return new Fixture(site.Id, supplier.Id, cement, customer.Id);
    }

    private static Task<ProjectDto> VillaAsync(IServiceProvider sp, Fixture f, string name,
        decimal estimate, decimal? sale, int percent, ProjectStatus status = ProjectStatus.Active,
        Guid? customerId = null) =>
        sp.GetRequiredService<IProjectService>().CreateAsync(new SaveProjectRequest(
            null, name, null, f.SiteId, customerId, null, null, null, null, null,
            estimate, sale, status, percent, null));

    /// <summary>Puts a known amount of cost on a villa by buying material straight to it.</summary>
    private static async Task SpendAsync(IServiceProvider sp, Fixture f, Guid projectId, decimal amount)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, null, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(f.Cement.Id, f.Cement.UnitId, 1, amount, 0, 0, projectId)]));
        await sp.SubmitAndApproveAsync(created.Id);
    }

    private static decimal Num(IReadOnlyList<object?> row, int i) => Convert.ToDecimal(row[i]);

    // ── villa profitability ────────────────────────────────────────────────

    [Fact]
    public async Task A_half_built_villa_earns_half_its_sale_value()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 104", 4_400_000, 5_800_000, 50, customerId: f.CustomerId);
        await SpendAsync(sp, f, villa.Id, 862_000);

        var table = await sp.GetRequiredService<IReportsService>().VillaProfitabilityAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Contracted Sale")).Should().Be(5_800_000);
        Num(row, col("Earned Revenue")).Should().Be(2_900_000, "half a villa has earned half its price");
        Num(row, col("Cost To Date")).Should().Be(862_000);
        Num(row, col("Earned Margin")).Should().Be(2_038_000,
            "the old margin would have reported 49.38L by crediting the whole sale value");
    }

    [Fact]
    public async Task A_finished_villa_earns_all_of_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 101", 4_200_000, 5_600_000, 100, ProjectStatus.Completed);
        await SpendAsync(sp, f, villa.Id, 1_999_000);

        var table = await sp.GetRequiredService<IReportsService>().VillaProfitabilityAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Earned Revenue")).Should().Be(5_600_000);
        Num(row, col("Earned Margin")).Should().Be(3_601_000);
    }

    [Fact]
    public async Task A_villa_handed_over_with_money_owed_is_flagged()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 103", 4_500_000, 5_900_000, 100,
            ProjectStatus.Completed, f.CustomerId);

        var methodId = await db.PaymentMethods.Select(m => m.Id).FirstAsync();
        await sp.GetRequiredService<ICustomerPaymentService>().CreateAsync(
            new SaveCustomerPaymentRequest(villa.Id, Today, 4_425_000, methodId, "NEFT/1", null));

        var table = await sp.GetRequiredService<IReportsService>().VillaProfitabilityAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Outstanding")).Should().Be(1_475_000);
        row[col("Flag")].Should().Be("DUES ON HANDOVER",
            "a finished villa with money owed is the most urgent line on the sheet");
    }

    [Fact]
    public async Task An_unsold_villa_reports_no_margin_rather_than_a_loss()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 106", 4_600_000, null, 50);
        await SpendAsync(sp, f, villa.Id, 862_000);

        var table = await sp.GetRequiredService<IReportsService>().VillaProfitabilityAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        row[col("Earned Margin")].Should().BeNull("there is no price to earn against yet");
        row[col("Flag")].Should().Be("unsold");
    }

    // ── budget burn ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_villa_spending_faster_than_it_builds_is_flagged_over_budget()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        // 10% of a 39L villa should have cost 3.9L. It has cost 4.27L.
        var villa = await VillaAsync(sp, f, "Villa 201", 3_900_000, 5_200_000, 10);
        await SpendAsync(sp, f, villa.Id, 427_000);

        var table = await sp.GetRequiredService<IReportsService>().BudgetBurnAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Expected By Now")).Should().Be(390_000);
        Num(row, col("Spent")).Should().Be(427_000);
        Num(row, col("Burn %")).Should().Be(109);
        row[col("Flag")].Should().Be("watch", "over budget but not yet far enough to shout about");

        Num(row, col("Left In Budget")).Should().Be(3_473_000);
    }

    [Fact]
    public async Task A_villa_well_inside_its_budget_carries_no_flag()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 104", 4_400_000, 5_800_000, 50);
        await SpendAsync(sp, f, villa.Id, 862_000);

        var table = await sp.GetRequiredService<IReportsService>().BudgetBurnAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Burn %")).Should().Be(39);
        row[col("Flag")].Should().Be("");
    }

    [Fact]
    public async Task A_villa_that_has_not_started_is_not_reported_as_over_budget()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        await VillaAsync(sp, f, "Villa 204", 4_000_000, null, 0, ProjectStatus.Planned);

        var table = await sp.GetRequiredService<IReportsService>().BudgetBurnAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        row[col("Burn %")].Should().BeNull("dividing by nothing built is not a budget overrun");
        row[col("Flag")].Should().Be("not started");
    }

    // ── site summary ───────────────────────────────────────────────────────

    [Fact]
    public async Task Site_summary_counts_build_cost_and_the_stock_still_on_its_shelves()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var villa = await VillaAsync(sp, f, "Villa 101", 4_200_000, 5_600_000, 100, ProjectStatus.Completed);
        await SpendAsync(sp, f, villa.Id, 800_000);          // straight to the villa
        await VillaAsync(sp, f, "Villa 106", 4_600_000, null, 50);

        // …and a lot that stayed in the store.
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var stockBuy = await purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, null, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(f.Cement.Id, f.Cement.UnitId, 500, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(stockBuy.Id);

        // The watchman belongs to the site, not to Villa 101.
        await sp.GetRequiredService<Application.Expenses.ISiteExpenseService>().CreateAsync(
            new Application.Expenses.SaveSiteExpenseRequest(
                f.SiteId, Today, await db.ExpenseHeads.Select(h => h.Id).FirstAsync(),
                "Watchman, three months", 45_000, PaymentStatus.Paid, null));

        var table = await sp.GetRequiredService<IReportsService>().SiteSummaryAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Villas")).Should().Be(2);
        Num(row, col("Unsold")).Should().Be(1);
        Num(row, col("Villa Cost")).Should().Be(800_000);
        Num(row, col("Site Overhead")).Should().Be(45_000);
        Num(row, col("Stock Value")).Should().Be(200_000);
        Num(row, col("Capital Employed")).Should().Be(1_045_000,
            "what the site has swallowed is what it built, its own overhead, and what is on its shelves");
    }

    [Fact]
    public async Task Site_overhead_stays_out_of_every_villa_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);
        var villa = await VillaAsync(sp, f, "Villa 101", 4_200_000, 5_600_000, 50);
        await SpendAsync(sp, f, villa.Id, 800_000);

        await sp.GetRequiredService<Application.Expenses.ISiteExpenseService>().CreateAsync(
            new Application.Expenses.SaveSiteExpenseRequest(
                f.SiteId, Today, await db.ExpenseHeads.Select(h => h.Id).FirstAsync(),
                "Temporary power connection", 60_000, PaymentStatus.Paid, null));

        var summary = await sp.GetRequiredService<IProjectService>().SummaryAsync(villa.Id);
        summary.TotalCost.Should().Be(800_000,
            "a villa's cost is exactly what was spent on that villa — dumping site overhead on it " +
            "would make one villa look expensive and its neighbours cheap");

        var company = await sp.GetRequiredService<IReportsService>().CompanySummaryAsync();
        var overheadRow = company.Rows.Single(r => (string)r[0]! == "Site overhead (not on any villa)");
        Convert.ToDecimal(overheadRow[1]).Should().Be(60_000, "but it is still the company's money");
    }

    // ── contractor commitment ──────────────────────────────────────────────

    [Fact]
    public async Task Contractor_commitment_shows_what_is_promised_but_unpaid()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);
        var villa = await VillaAsync(sp, f, "Villa 104", 4_400_000, 5_800_000, 50);

        var contractor = new Contractor { Code = "CON-1", Name = "Kumar Masonry" };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync();

        await sp.GetRequiredService<Application.Contractors.IContractWorkService>().CreateAsync(
            new Application.Contractors.SaveContractWorkRequest(
                villa.Id, contractor.Id, "Masonry", null, 620_000, 640_000,
                Today, null, null, ContractWorkStatus.Active));

        var table = await sp.GetRequiredService<IReportsService>().ContractorCommitmentAsync();
        var row = table.Rows.Single();
        var col = (string c) => table.Columns.ToList().IndexOf(c);

        Num(row, col("Contracted")).Should().Be(640_000);
        Num(row, col("Paid")).Should().Be(0);
        Num(row, col("Committed Unpaid")).Should().Be(640_000,
            "money promised under a work order is not in the villa's cost, and someone has to see it");
    }
}
