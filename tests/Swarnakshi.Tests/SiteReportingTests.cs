using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Dashboard;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Reports;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Sites, lookup masters, dashboard KPIs and reports. The read-only aggregations matter because a
/// wrong figure here silently misinforms the owner — so they are asserted against known data.
/// </summary>
public class SiteReportingTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static SaveSiteRequest NewSite(string code, string name, SiteStatus status = SiteStatus.Active)
        => new(code, name, null, "Hyderabad", "Telangana", "500001", null, Today, status, null);

    // ---- sites -----------------------------------------------------------

    [Fact]
    public async Task Creates_a_site_and_reads_it_back()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var created = await sites.CreateAsync(NewSite("GV", "Green Valley"));
        var fetched = await sites.GetAsync(created.Id);

        fetched.Code.Should().Be("GV");
        fetched.Name.Should().Be("Green Valley");
        fetched.Status.Should().Be(SiteStatus.Active);
    }

    [Fact]
    public async Task Duplicate_site_code_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        await sites.CreateAsync(NewSite("GV", "Green Valley"));
        var act = () => sites.CreateAsync(NewSite("GV", "Another Site"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Updating_a_site_keeps_its_id_and_changes_status()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var created = await sites.CreateAsync(NewSite("GV", "Green Valley"));
        var updated = await sites.UpdateAsync(created.Id,
            NewSite("GV", "Green Valley Phase 2", SiteStatus.OnHold));

        updated.Id.Should().Be(created.Id);
        updated.Name.Should().Be("Green Valley Phase 2");
        updated.Status.Should().Be(SiteStatus.OnHold);
    }

    [Fact]
    public async Task Sites_can_be_filtered_by_status()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        await sites.CreateAsync(NewSite("A1", "Active Site"));
        await sites.CreateAsync(NewSite("H1", "Held Site", SiteStatus.OnHold));

        var active = await sites.ListAsync(new PageQuery { PageSize = 50 }, SiteStatus.Active);
        var held = await sites.ListAsync(new PageQuery { PageSize = 50 }, SiteStatus.OnHold);

        active.Items.Should().ContainSingle(s => s.Code == "A1");
        held.Items.Should().ContainSingle(s => s.Code == "H1");
    }

    [Fact]
    public async Task An_unknown_site_id_is_a_not_found()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var act = () => sites.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---- lookup masters --------------------------------------------------

    [Fact]
    public async Task Lookup_lists_are_seeded_and_returned()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var masters = scope.ServiceProvider.GetRequiredService<IMasterService>();

        (await masters.UnitsAsync()).Should().NotBeEmpty();
        (await masters.MaterialCategoriesAsync()).Should().HaveCount(10, "nine trades plus a General catch-all");
        (await masters.ExpenseHeadsAsync()).Should().NotBeEmpty();
        (await masters.LabourCategoriesAsync()).Should().NotBeEmpty();
        (await masters.PaymentMethodsAsync()).Should().NotBeEmpty();
        (await masters.ProjectTypesAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Subcategories_can_be_scoped_to_their_category()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var masters = sp.GetRequiredService<IMasterService>();

        var civil = await db.MaterialCategories.FirstAsync(c => c.Name == "Civil & Structure");

        var all = await masters.MaterialSubcategoriesAsync(null);
        var scoped = await masters.MaterialSubcategoriesAsync(civil.Id);

        scoped.Should().OnlyContain(s => s.ParentId == civil.Id);
        scoped.Count.Should().BeLessThan(all.Count);
        scoped.Select(s => s.Name).Should().Contain("OPC Cement");
    }

    // ---- dashboard -------------------------------------------------------

    [Fact]
    public async Task Dashboard_reflects_real_data_rather_than_placeholders()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var dash = sp.GetRequiredService<IDashboardService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        db.Sites.Add(site);
        db.Projects.Add(new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active });
        await db.SaveChangesAsync();

        var payload = await dash.GetAsync();

        payload.Kpis.Should().NotBeEmpty();
        payload.Kpis.Should().OnlyContain(k => k.Format == "money" || k.Format == "count");
        payload.PendingApprovals.Should().Be(0);
        payload.Role.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Dashboard_inventory_value_matches_the_posted_purchase()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var dash = sp.GetRequiredService<IDashboardService>();
        var purchases = sp.GetRequiredService<IPurchaseService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.AddRange(site, supplier);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, site.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 100, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var payload = await dash.GetAsync();
        var inventoryKpi = payload.Kpis.FirstOrDefault(k => k.Label.Contains("Inventory", StringComparison.OrdinalIgnoreCase));

        inventoryKpi.Should().NotBeNull();
        inventoryKpi!.Value.Should().Be(40_000m);
    }

    // ---- reports ---------------------------------------------------------

    [Fact]
    public async Task Every_report_returns_a_well_formed_table()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportsService>();

        var tables = new[]
        {
            await reports.InventoryStockAsync(null),
            await reports.PurchaseRegisterAsync(null, null, null),
            await reports.ConsumptionRegisterAsync(null, null, null),
            await reports.LowStockAsync(),
            await reports.ProjectCostSummaryAsync(),
            await reports.ContractorOutstandingAsync(),
            await reports.CustomerOutstandingAsync(),
            await reports.CompanySummaryAsync(),
        };

        foreach (var t in tables)
        {
            t.Title.Should().NotBeNullOrWhiteSpace();
            t.Columns.Should().NotBeEmpty();
            // every row must line up with the declared columns, or the UI renders ragged
            foreach (var row in t.Rows)
                row.Count.Should().Be(t.Columns.Count, $"row width must match columns in '{t.Title}'");
        }
    }

    [Fact]
    public async Task Inventory_stock_report_shows_the_posted_purchase()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var reports = sp.GetRequiredService<IReportsService>();
        var purchases = sp.GetRequiredService<IPurchaseService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.AddRange(site, supplier);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, site.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 60, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var stock = await reports.InventoryStockAsync(site.Id);
        var register = await reports.PurchaseRegisterAsync(null, null, site.Id);

        stock.Rows.Should().NotBeEmpty();
        stock.Rows.Should().Contain(r => r.Any(c => c != null && c.ToString()!.Contains("MAT-CEM-OPC")));
        register.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Inventory_stock_report_is_scoped_to_the_requested_site()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var reports = sp.GetRequiredService<IReportsService>();
        var purchases = sp.GetRequiredService<IPurchaseService>();

        var siteA = new Site { Code = "S1", Name = "Site A", Status = SiteStatus.Active };
        var siteB = new Site { Code = "S2", Name = "Site B", Status = SiteStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.AddRange(siteA, siteB, supplier);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, siteA.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 60, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var atA = await reports.InventoryStockAsync(siteA.Id);
        var atB = await reports.InventoryStockAsync(siteB.Id);

        atA.Rows.Should().NotBeEmpty();
        atB.Rows.Should().BeEmpty("Site B never received stock — inventory is site-level");
    }

    [Fact]
    public async Task Low_stock_report_only_lists_materials_under_their_reorder_level()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var reports = sp.GetRequiredService<IReportsService>();
        var purchases = sp.GetRequiredService<IPurchaseService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.AddRange(site, supplier);
        await db.SaveChangesAsync();

        // Seed materials have reorder level 0, so nothing qualifies yet.
        (await reports.LowStockAsync()).Rows.Should().BeEmpty();

        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");
        // The report's threshold is MinStockLevel — the same field InventoryStockAsync flags as "Low".
        material.MinStockLevel = 500;            // far above what we will stock
        material.ReorderLevel = 600;
        await db.SaveChangesAsync();

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, site.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 10, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var low = await reports.LowStockAsync();

        low.Rows.Should().NotBeEmpty("10 in stock against a minimum level of 500 is low");
    }
}
