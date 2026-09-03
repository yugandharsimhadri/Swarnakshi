using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// A builder recording a delivery should be able to type the supplier's name and carry on, not
/// stop to create a master first. The purchase resolves the name: matches an existing supplier, or
/// makes a bare one.
/// </summary>
public class TypeableSupplierTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<(Guid SiteId, Material Cement)> ArrangeAsync(IServiceProvider sp, AppDbContext db)
    {
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(
            new SaveSiteRequest(null, "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        var cement = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return (site.Id, cement);
    }

    private static SavePurchaseRequest Buy(Guid? supplierId, string? supplierName, Guid siteId, Material cement) =>
        new(supplierId, supplierName, siteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(cement.Id, cement.UnitId, 100, 400, 0, 0)]);

    [Fact]
    public async Task Typing_a_new_name_creates_the_supplier_and_the_purchase_points_at_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var (siteId, cement) = await ArrangeAsync(sp, db);

        var created = await sp.GetRequiredService<IPurchaseService>()
            .CreateAsync(Buy(null, "  Sri Balaji Traders  ", siteId, cement));

        created.SupplierName.Should().Be("Sri Balaji Traders", "trimmed");
        var supplier = await db.Suppliers.SingleAsync(s => s.Name == "Sri Balaji Traders");
        supplier.Id.Should().Be(created.SupplierId);
        supplier.Code.Should().StartWith("SUP-", "it gets an auto code like any other master");
        supplier.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Typing_a_name_that_already_exists_reuses_it_rather_than_making_a_duplicate()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var (siteId, cement) = await ArrangeAsync(sp, db);
        var purchases = sp.GetRequiredService<IPurchaseService>();

        var first = await purchases.CreateAsync(Buy(null, "Anjaneya Hardware", siteId, cement));
        var second = await purchases.CreateAsync(Buy(null, "anjaneya hardware", siteId, cement)); // different case

        second.SupplierId.Should().Be(first.SupplierId, "the case-insensitive match wins");
        (await db.Suppliers.CountAsync(s => s.Name.ToLower() == "anjaneya hardware")).Should().Be(1);
    }

    [Fact]
    public async Task Picking_an_existing_supplier_by_id_still_works()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var (siteId, cement) = await ArrangeAsync(sp, db);

        var supplier = new Supplier { Code = "SUP-9", Name = "Lakshmi Traders" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var created = await sp.GetRequiredService<IPurchaseService>()
            .CreateAsync(Buy(supplier.Id, null, siteId, cement));

        created.SupplierId.Should().Be(supplier.Id);
        created.SupplierName.Should().Be("Lakshmi Traders");
    }

    [Fact]
    public async Task A_purchase_with_neither_an_id_nor_a_name_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var (siteId, cement) = await ArrangeAsync(sp, db);

        var act = () => sp.GetRequiredService<IPurchaseService>()
            .CreateAsync(Buy(null, "   ", siteId, cement));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*supplier*");
    }
}
