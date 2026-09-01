using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Application.Projects;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Service-level inventory operations — opening stock, adjustments and returns from a project.
/// These move both inventory value and project cost, and were previously untested end to end
/// (InventoryBalanceTests covers only the entity arithmetic).
/// </summary>
public class InventoryOperationsTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<(Site Site, Project Project, Material Material)> ArrangeAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
        db.AddRange(site, project);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return (site, project, material);
    }

    // ---- opening stock ---------------------------------------------------

    [Fact]
    public async Task Opening_stock_creates_the_balance_at_the_stated_rate()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, "carried forward"));

        var b = await db.InventoryBalances.AsNoTracking().SingleAsync(x => x.MaterialId == material.Id);
        b.Quantity.Should().Be(100);
        b.AverageRate.Should().Be(400);
        b.Value.Should().Be(40_000);
    }

    [Fact]
    public async Task Opening_stock_does_not_touch_project_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inv = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var (site, project, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 50, 400, Today, null));

        // Stock arriving at a site is inventory value, never a project cost.
        (await projects.SummaryAsync(project.Id)).MaterialCost.Should().Be(0);
    }

    // ---- adjustments -----------------------------------------------------

    [Fact]
    public async Task Positive_adjustment_increases_quantity_and_value()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));
        await inv.AdjustmentAsync(new AdjustmentRequest(site.Id, material.Id, 10, null, Today, "found extra bags"));

        var b = await db.InventoryBalances.AsNoTracking().SingleAsync(x => x.MaterialId == material.Id);
        b.Quantity.Should().Be(110);
        b.AverageRate.Should().Be(400, "topping up at the current average must not move the rate");
        b.Value.Should().Be(44_000);
    }

    [Fact]
    public async Task Negative_adjustment_reduces_quantity_at_the_current_average()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));
        await inv.AdjustmentAsync(new AdjustmentRequest(site.Id, material.Id, -25, null, Today, "damaged in transit"));

        var b = await db.InventoryBalances.AsNoTracking().SingleAsync(x => x.MaterialId == material.Id);
        b.Quantity.Should().Be(75);
        b.Value.Should().Be(30_000);
    }

    [Fact]
    public async Task Adjustment_writes_a_traceable_ledger_row_carrying_the_reason()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));
        await inv.AdjustmentAsync(new AdjustmentRequest(site.Id, material.Id, -5, null, Today, "spillage"));

        var txn = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.MaterialId == material.Id && t.Type == InventoryTransactionType.Adjustment)
            .SingleAsync();

        txn.Remarks.Should().Be("spillage");
        txn.Quantity.Should().Be(-5, "issues are stored signed negative");
        txn.TxnNumber.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Adjustment_is_refused_for_a_user_without_approval_rights()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);
        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));

        // Drop to a supervisor-like permission set: no approvals.decide.
        host.CurrentUser.SetUser(host.CurrentUser.UserId!.Value, UserRole.Supervisor,
            [Swarnakshi.Application.Security.Permissions.InventoryView,
             Swarnakshi.Application.Security.Permissions.InventoryAdjust]);

        var act = () => inv.AdjustmentAsync(new AdjustmentRequest(site.Id, material.Id, -5, null, Today, "shrinkage"));

        await act.Should().ThrowAsync<ForbiddenException>();
        (await db.InventoryBalances.AsNoTracking().SingleAsync(x => x.MaterialId == material.Id))
            .Quantity.Should().Be(100, "the refused adjustment must not have touched stock");
    }

    // ---- returns from a project -----------------------------------------

    [Fact]
    public async Task Return_from_project_adds_stock_back_and_reverses_the_material_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inv = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var (site, project, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));
        // consume 40 into the project
        await inv.IssueAsync(site.Id, material.Id, material.UnitId, 40,
            InventoryTransactionType.ProjectConsumption, Today, "Test", Guid.Empty, null, project.Id,
            null, host.CurrentUser.UserId!.Value, default);
        await db.SaveChangesAsync();
        await sp.GetRequiredService<IProjectCostWriter>().WriteMaterialCostAsync(
            project.Id, 40 * 400m, Today, null, null, "Test", Guid.Empty, "consumption");
        await db.SaveChangesAsync();

        var costBefore = (await projects.SummaryAsync(project.Id)).MaterialCost;
        costBefore.Should().Be(16_000);

        await inv.ReturnFromProjectAsync(new ReturnRequest(site.Id, project.Id, material.Id, 10, Today, "unused"));

        var b = await db.InventoryBalances.AsNoTracking().SingleAsync(x => x.MaterialId == material.Id);
        b.Quantity.Should().Be(70, "60 remaining + 10 returned");

        var costAfter = (await projects.SummaryAsync(project.Id)).MaterialCost;
        costAfter.Should().Be(12_000, "returning 10 @ 400 reverses 4,000 of project cost");
    }

    [Fact]
    public async Task Return_is_rejected_when_the_project_belongs_to_another_site()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, project, material) = await ArrangeAsync(db);

        var otherSite = new Site { Code = "S2", Name = "Site 2", Status = SiteStatus.Active };
        db.Sites.Add(otherSite);
        await db.SaveChangesAsync();

        var act = () => inv.ReturnFromProjectAsync(
            new ReturnRequest(otherSite.Id, project.Id, material.Id, 5, Today, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*does not belong to this site*");
    }

    [Fact]
    public async Task Return_of_a_non_positive_quantity_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, project, material) = await ArrangeAsync(db);

        var act = () => inv.ReturnFromProjectAsync(new ReturnRequest(site.Id, project.Id, material.Id, 0, Today, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*must be positive*");
    }

    // ---- balances & ledger reads ----------------------------------------

    [Fact]
    public async Task Balances_are_scoped_to_a_site_never_pooled_across_sites()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (siteA, _, material) = await ArrangeAsync(db);

        var siteB = new Site { Code = "S2", Name = "Site 2", Status = SiteStatus.Active };
        db.Sites.Add(siteB);
        await db.SaveChangesAsync();

        await inv.OpeningStockAsync(new OpeningStockRequest(siteA.Id, material.Id, 100, 400, Today, null));
        await inv.OpeningStockAsync(new OpeningStockRequest(siteB.Id, material.Id, 30, 500, Today, null));

        var a = await inv.BalancesAsync(siteA.Id, null, false, null);
        var b = await inv.BalancesAsync(siteB.Id, null, false, null);

        a.Single(x => x.MaterialId == material.Id).Quantity.Should().Be(100);
        b.Single(x => x.MaterialId == material.Id).Quantity.Should().Be(30);
        b.Single(x => x.MaterialId == material.Id).AverageRate.Should().Be(500);
    }

    [Fact]
    public async Task Ledger_can_be_filtered_by_material_and_type()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 100, 400, Today, null));
        await inv.AdjustmentAsync(new AdjustmentRequest(site.Id, material.Id, -5, null, Today, "spillage"));

        var all = await inv.LedgerAsync(new PageQuery { PageSize = 50 }, site.Id, material.Id, null, null, null, null);
        var adjustments = await inv.LedgerAsync(new PageQuery { PageSize = 50 }, site.Id, material.Id,
            null, InventoryTransactionType.Adjustment, null, null);

        all.Total.Should().Be(2);
        adjustments.Total.Should().Be(1);
        adjustments.Items[0].Type.Should().Be(InventoryTransactionType.Adjustment);
    }

    [Fact]
    public async Task Material_detail_reports_stock_for_the_requested_site()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var (site, _, material) = await ArrangeAsync(db);

        await inv.OpeningStockAsync(new OpeningStockRequest(site.Id, material.Id, 80, 425, Today, null));

        var detail = await inv.MaterialDetailAsync(site.Id, material.Id);

        detail.MaterialId.Should().Be(material.Id);
        detail.SiteId.Should().Be(site.Id);
    }
}
