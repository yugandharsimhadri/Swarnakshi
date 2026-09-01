using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// "100 bags of cement bought for Villa 101, delivered straight there."
///
/// The material still passes THROUGH site inventory — received, then immediately issued — so the
/// stock ledger tells the whole story and purchases reconcile against consumption. These tests pin
/// down the arithmetic that makes that safe.
/// </summary>
public class DirectToProjectPurchaseTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid SiteId, Guid ProjectId, Guid SupplierId, Material Material);

    private static async Task<Fixture> ArrangeAsync(IServiceProvider sp, AppDbContext db)
    {
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();

        var site = await sites.CreateAsync(new SaveSiteRequest("GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        var project = await projects.CreateAsync(new SaveProjectRequest("GV-101", "Villa 101", "101", site.Id, null, null, null, null, null, null, 5_000_000, null, ProjectStatus.Active, null));

        var supplier = new Supplier { Code = "SUP-1", Name = "Sri Balaji Traders" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var material = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return new Fixture(site.Id, project.Id, supplier.Id, material);
    }

    private static async Task<PurchaseDto> BuyAsync(IServiceProvider sp, Fixture f,
        decimal qty, decimal rate, Guid? deliverTo)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(f.Material.Id, f.Material.UnitId, qty, rate, 0, 0, deliverTo)]));
        return await purchases.SubmitAsync(created.Id);
    }

    [Fact]
    public async Task Buying_100_bags_for_a_villa_charges_the_villa_and_leaves_the_store_untouched()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var f = await ArrangeAsync(sp, db);

        // The store already holds stock at a different rate — the case where a blended average
        // would quietly charge the villa the wrong number.
        await BuyAsync(sp, f, 200, 400, deliverTo: null);
        var before = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == f.Material.Id);
        before.Quantity.Should().Be(200);
        before.AverageRate.Should().Be(400);
        before.Value.Should().Be(80_000);

        // 100 bags at ₹450 bought for Villa 101 and taken straight there.
        await BuyAsync(sp, f, 100, 450, deliverTo: f.ProjectId);

        var after = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == f.Material.Id);
        after.Quantity.Should().Be(200, "what passed through was issued out again in the same post");
        after.AverageRate.Should().Be(400, "earmarked material must not distort the pool's valuation");
        after.Value.Should().Be(80_000);

        var summary = await projects.SummaryAsync(f.ProjectId);
        summary.MaterialCost.Should().Be(45_000, "the villa is charged what was actually paid for its material");

        // Both movements are on the ledger — the trail a builder can follow.
        var ledger = await inventory.LedgerAsync(new PageQuery { PageSize = 50 }, f.SiteId, f.Material.Id, null, null, null, null);
        ledger.Items.Should().Contain(t => t.Type == InventoryTransactionType.PurchaseReceipt && t.Quantity == 100 && t.Rate == 450);
        ledger.Items.Should().Contain(t => t.Type == InventoryTransactionType.ProjectConsumption && t.Quantity == -100 && t.Rate == 450
                                           && t.ProjectName == "Villa 101");
    }

    [Fact]
    public async Task Purchase_value_still_equals_consumed_cost_plus_stock_on_hand()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var projects = sp.GetRequiredService<IProjectService>();
        var f = await ArrangeAsync(sp, db);

        await BuyAsync(sp, f, 200, 400, deliverTo: null);        // ₹80,000 into stock
        await BuyAsync(sp, f, 100, 450, deliverTo: f.ProjectId); // ₹45,000 straight to the villa
        var purchased = 200 * 400m + 100 * 450m;

        var stock = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == f.Material.Id);
        var consumed = (await projects.SummaryAsync(f.ProjectId)).MaterialCost;

        (consumed + stock.Value).Should().Be(purchased,
            "the no-double-counting identity has to survive the direct-delivery shortcut");
    }

    [Fact]
    public async Task A_direct_delivery_into_an_empty_store_works_and_leaves_nothing_behind()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var projects = sp.GetRequiredService<IProjectService>();
        var f = await ArrangeAsync(sp, db);

        await BuyAsync(sp, f, 100, 420, deliverTo: f.ProjectId);

        var stock = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == f.Material.Id);
        stock.Quantity.Should().Be(0);
        stock.Value.Should().Be(0);
        (await projects.SummaryAsync(f.ProjectId)).MaterialCost.Should().Be(42_000);
    }

    [Fact]
    public async Task One_invoice_can_split_between_the_store_and_a_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var f = await ArrangeAsync(sp, db);

        var steel = await db.Materials.FirstAsync(m => m.Code == "MAT-STL-TMT");

        // Entered as one delivery: cement earmarked for the villa, steel into the common pool.
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, f.SiteId, null, "INV-77", null, Today, 0, null,
            [
                new PurchaseItemInput(f.Material.Id, f.Material.UnitId, 100, 450, 0, 0, f.ProjectId),
                new PurchaseItemInput(steel.Id, steel.UnitId, 500, 68, 0, 0, null),
            ]));
        await purchases.SubmitAsync(created.Id);

        (await projects.SummaryAsync(f.ProjectId)).MaterialCost.Should().Be(45_000, "only the cement was earmarked");

        var cementStock = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == f.Material.Id);
        var steelStock = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == steel.Id);
        cementStock.Quantity.Should().Be(0);
        steelStock.Quantity.Should().Be(500);
        steelStock.Value.Should().Be(34_000);
    }

    [Fact]
    public async Task Tax_and_discount_land_in_the_villas_cost_because_the_landed_rate_is_used()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var f = await ArrangeAsync(sp, db);

        // 100 × ₹400 = ₹40,000, less ₹1,000 discount, plus ₹2,000 tax → ₹41,000 landed.
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(f.Material.Id, f.Material.UnitId, 100, 400, 1_000, 2_000, f.ProjectId)]));
        await purchases.SubmitAsync(created.Id);

        (await projects.SummaryAsync(f.ProjectId)).MaterialCost.Should().Be(41_000,
            "the villa bears the delivered cost, not the headline rate");
    }

    [Fact]
    public async Task A_project_on_another_site_cannot_be_the_delivery_target()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var f = await ArrangeAsync(sp, db);

        var otherSite = await sites.CreateAsync(new SaveSiteRequest("SR", "Sunrise", null, null, null, null, null, null, SiteStatus.Active, null));
        var elsewhere = await projects.CreateAsync(new SaveProjectRequest("SR-1", "Villa 900", null, otherSite.Id, null, null, null, null, null, null, 100_000, null, ProjectStatus.Active, null));

        var act = () => purchases.CreateAsync(new SavePurchaseRequest(
            f.SupplierId, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(f.Material.Id, f.Material.UnitId, 10, 400, 0, 0, elsewhere.Id)]));

        // Inventory is site-level; this would otherwise issue from a store the project cannot draw on.
        await act.Should().ThrowAsync<AppException>().WithMessage("*not on this site*");
    }
}
