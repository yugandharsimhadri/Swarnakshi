using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Material Master: identity, duplicate prevention, code locking, lifecycle and search.
/// Also guards that the redesign left the existing procurement/inventory relationships intact.
/// </summary>
public class MaterialMasterTests
{
    // ---- helpers ---------------------------------------------------------

    private static async Task<(Guid SubId, Guid UnitId)> WireSubcategoryAsync(AppDbContext db,
        string category, string subcategory, string unitCode = "MTR")
    {
        var sub = await db.MaterialSubcategories.Include(s => s.Category)
            .FirstAsync(s => s.Category.Name == category && s.Name == subcategory);
        var unit = await db.Units.FirstAsync(u => u.Code == unitCode);
        return (sub.Id, unit.Id);
    }

    private static SaveMaterialRequest Wire(string code, string name, Guid subId, Guid unitId,
        string? brand, string size = "2.5", string sizeUnit = "sq.mm")
        => new(code, name, subId, brand, unitId, null, null, "90 Meter / Coil",
            0, 0, 55, 18, null, null,
            new Dictionary<string, string?> { ["size"] = size, ["size_unit"] = sizeUnit });

    // ---- creation & identity --------------------------------------------

    [Fact]
    public async Task Creates_material_with_brand_specs_and_generated_summary()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var created = await svc.CreateAsync(Wire("ELE-WIR-POL-025", "Electrical Wire", subId, unitId, "Polycab"));

        created.Brand.Should().Be("Polycab");
        created.SpecSummary.Should().Be("2.5 sq.mm");
        created.GenericMeasurement.Should().Be("90 Meter / Coil");
        created.Specifications.Should().HaveCount(2);
        created.IsActive.Should().BeTrue();
        created.CodeLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Blocks_exact_duplicate_name_brand_and_specification()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));

        var act = () => svc.CreateAsync(Wire("ELE-2", "Electrical Wire", subId, unitId, "Polycab"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Allows_same_material_and_brand_with_a_different_specification()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab", "2.5"));

        var other = await svc.CreateAsync(Wire("ELE-2", "Electrical Wire", subId, unitId, "Polycab", "4"));

        other.SpecSummary.Should().Be("4 sq.mm");
    }

    [Fact]
    public async Task Allows_same_material_and_specification_from_a_different_company()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));

        var finolex = await svc.CreateAsync(Wire("ELE-2", "Electrical Wire", subId, unitId, "Finolex"));

        finolex.Brand.Should().Be("Finolex");
    }

    [Fact]
    public async Task Rejects_a_specification_that_the_subcategory_does_not_declare()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var req = new SaveMaterialRequest("ELE-9", "Electrical Wire", subId, "Polycab", unitId,
            null, null, null, 0, 0, 10, null, null, null,
            new Dictionary<string, string?> { ["size"] = "2.5", ["size_unit"] = "sq.mm", ["grade"] = "Fe500" });

        var act = () => svc.CreateAsync(req);

        await act.Should().ThrowAsync<AppException>().WithMessage("*not applicable*");
    }

    // ---- search ----------------------------------------------------------

    [Fact]
    public async Task Search_matches_company_and_specification_values()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab", "2.5"));

        var byBrand = await svc.ListAsync(new PageQuery { Q = "Polycab" }, null, null, null, null, null);
        var bySpec = await svc.ListAsync(new PageQuery { Q = "2.5" }, null, null, null, null, null);
        var bySummary = await svc.ListAsync(new PageQuery { Q = "2.5 sq.mm" }, null, null, null, null, null);
        var byCategory = await svc.ListAsync(new PageQuery { Q = "Cement" }, null, null, null, null, null);

        byBrand.Items.Should().ContainSingle(m => m.Code == "ELE-1");
        bySpec.Items.Should().Contain(m => m.Code == "ELE-1");
        bySummary.Items.Should().ContainSingle(m => m.Code == "ELE-1");
        byCategory.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));

        // EF maps string.Contains to SQLite's case-sensitive instr(); these would all miss without
        // the explicit lowering in ListAsync.
        var lower = await svc.ListAsync(new PageQuery { Q = "cement" }, null, null, null, null, null);
        var upper = await svc.ListAsync(new PageQuery { Q = "CEMENT" }, null, null, null, null, null);
        var brandLower = await svc.ListAsync(new PageQuery { Q = "polycab" }, null, null, null, null, null);

        lower.Total.Should().BeGreaterThan(0);
        upper.Total.Should().Be(lower.Total);
        brandLower.Items.Should().Contain(m => m.Code == "ELE-1");
    }

    // ---- edit & code locking --------------------------------------------

    [Fact]
    public async Task Code_is_editable_while_the_material_is_unused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var m = await svc.CreateAsync(Wire("ELE-OLD", "Electrical Wire", subId, unitId, "Polycab"));

        var updated = await svc.UpdateAsync(m.Id, Wire("ELE-NEW", "Electrical Wire", subId, unitId, "Polycab"));

        updated.Code.Should().Be("ELE-NEW");
        updated.CodeLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Code_is_locked_once_a_transaction_references_the_material()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IMaterialService>();

        var (site, _, material) = await SeedPurchaseAsync(sp, db, 10, 100);

        var detail = await svc.GetAsync(material.Id);
        detail.CodeLocked.Should().BeTrue();

        var act = () => svc.UpdateAsync(material.Id, new SaveMaterialRequest(
            "CHANGED", material.Name, material.MaterialSubcategoryId, null, material.UnitId,
            null, null, null, 0, 0, 0, null, null, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*can no longer be changed*");
        site.Should().NotBeNull();
    }

    [Fact]
    public async Task Non_code_fields_stay_editable_after_the_material_is_used()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IMaterialService>();

        var (_, _, material) = await SeedPurchaseAsync(sp, db, 10, 100);

        var updated = await svc.UpdateAsync(material.Id, new SaveMaterialRequest(
            material.Code, material.Name, material.MaterialSubcategoryId, "Ultratech", material.UnitId,
            null, null, "50 Kg / Bag", 5, 10, 450, 28, "desc", "notes", null));

        updated.Brand.Should().Be("Ultratech");
        updated.GenericMeasurement.Should().Be("50 Kg / Bag");
        updated.ReorderLevel.Should().Be(10);
    }

    // ---- lifecycle -------------------------------------------------------

    [Fact]
    public async Task Deactivates_a_material_with_no_stock_then_reactivates_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var m = await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));

        var off = await svc.DeactivateAsync(m.Id);
        off.IsActive.Should().BeFalse();

        var on = await svc.ReactivateAsync(m.Id);
        on.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivation_is_blocked_server_side_while_stock_exists()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IMaterialService>();

        var (_, _, material) = await SeedPurchaseAsync(sp, db, 25, 400);

        var act = () => svc.DeactivateAsync(material.Id);

        await act.Should().ThrowAsync<AppException>().WithMessage("*has stock at one or more sites*");
        (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == material.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivation_leaves_inventory_and_history_untouched()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IMaterialService>();

        var (_, _, material) = await SeedPurchaseAsync(sp, db, 25, 400);

        // drain the stock so deactivation is allowed, then confirm history survives
        var balance = await db.InventoryBalances.FirstAsync(b => b.MaterialId == material.Id);
        balance.Quantity = 0;
        balance.Value = 0;
        await db.SaveChangesAsync();

        await svc.DeactivateAsync(material.Id);

        (await db.InventoryTransactions.CountAsync(t => t.MaterialId == material.Id)).Should().BeGreaterThan(0);
        (await db.PurchaseItems.CountAsync(p => p.MaterialId == material.Id)).Should().BeGreaterThan(0);
        (await svc.GetAsync(material.Id)).Code.Should().Be(material.Code);
    }

    [Fact]
    public async Task Inactive_materials_are_excluded_from_active_selection_but_remain_retrievable()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var m = await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));
        await svc.DeactivateAsync(m.Id);

        var active = await svc.ListAsync(new PageQuery { PageSize = 200 }, null, null, null, null, true);
        var inactive = await svc.ListAsync(new PageQuery { PageSize = 200 }, null, null, null, null, false);

        active.Items.Should().NotContain(x => x.Id == m.Id);
        inactive.Items.Should().Contain(x => x.Id == m.Id);
        (await svc.GetAsync(m.Id)).Should().NotBeNull();     // history stays reachable
    }

    // ---- taxonomy & specification definitions ---------------------------

    [Fact]
    public async Task Seeds_the_fifty_approved_categories_with_the_separations_the_business_requires()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var names = await db.MaterialCategories.Where(c => c.IsActive).Select(c => c.Name).ToListAsync();

        names.Should().HaveCount(50);
        names.Should().Contain(["Sand", "Aggregates & Gravel"]);        // never merged
        names.Should().Contain(["Bricks", "Blocks"]);                   // never merged
        names.Should().Contain(["Granite", "Tiles"]);                   // never merged
        names.Should().Contain("Waterproofing Materials");              // its own category
        names.Should().NotContain("Construction Chemicals & Waterproofing");
    }

    [Fact]
    public async Task Specification_fields_are_scoped_to_the_subcategory()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (wireSub, _) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var (tmtSub, _) = await WireSubcategoryAsync(db, "Iron & Steel", "TMT Bars");

        var wireSpecs = await svc.SpecDefinitionsAsync(wireSub);
        var tmtSpecs = await svc.SpecDefinitionsAsync(tmtSub);

        wireSpecs.Select(s => s.Key).Should().BeEquivalentTo(["size", "size_unit"]);
        tmtSpecs.Select(s => s.Key).Should().BeEquivalentTo(["diameter", "diameter_unit", "grade"]);
        // Company/Brand is a Material column, never a spec field.
        wireSpecs.Should().NotContain(s => s.Key.Contains("brand"));
    }

    [Fact]
    public async Task Ms_steel_is_accepted_as_a_company_name()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var sub = await db.MaterialSubcategories.Include(s => s.Category)
            .FirstAsync(s => s.Category.Name == "Iron & Steel" && s.Name == "TMT Bars");
        var kg = await db.Units.FirstAsync(u => u.Code == "KG");

        var specs = new Dictionary<string, string?> { ["diameter"] = "12", ["diameter_unit"] = "mm", ["grade"] = "Fe500" };
        var tata = await svc.CreateAsync(new SaveMaterialRequest("STL-TATA-012", "TMT Bar", sub.Id, "Tata Steel",
            kg.Id, null, null, null, 0, 0, 68, 18, null, null, specs));
        var ms = await svc.CreateAsync(new SaveMaterialRequest("STL-MS-012", "TMT Bar", sub.Id, "MS Steel",
            kg.Id, null, null, null, 0, 0, 66, 18, null, null, specs));

        tata.SpecSummary.Should().Be("12 mm · Fe500");
        ms.Brand.Should().Be("MS Steel");
        ms.SpecSummary.Should().Be("12 mm · Fe500");
    }

    // ---- audit -----------------------------------------------------------

    [Fact]
    public async Task Writes_audit_rows_for_create_deactivate_and_reactivate()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IMaterialService>();

        var (subId, unitId) = await WireSubcategoryAsync(db, "Electrical Wire", "Single Core");
        var m = await svc.CreateAsync(Wire("ELE-1", "Electrical Wire", subId, unitId, "Polycab"));
        await svc.DeactivateAsync(m.Id);
        await svc.ReactivateAsync(m.Id);

        var actions = await db.AuditLogs.Where(a => a.EntityType == "Material" && a.EntityId == m.Id)
            .Select(a => a.Action).ToListAsync();

        actions.Should().BeEquivalentTo(["Material created", "Material deactivated", "Material reactivated"]);
    }

    // ---- regression: existing relationships still resolve ----------------

    [Fact]
    public async Task Existing_transaction_relationships_still_resolve_after_the_redesign()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IMaterialService>();

        var (site, _, material) = await SeedPurchaseAsync(sp, db, 100, 400);

        (await db.PurchaseItems.CountAsync(p => p.MaterialId == material.Id)).Should().Be(1);
        (await db.InventoryTransactions.CountAsync(t => t.MaterialId == material.Id)).Should().Be(1);
        (await db.InventoryBalances.CountAsync(b => b.MaterialId == material.Id)).Should().Be(1);

        // inventory valuation is unchanged by the Material Master redesign
        var balance = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == material.Id);
        balance.Quantity.Should().Be(100);
        balance.AverageRate.Should().Be(400);
        balance.Value.Should().Be(40_000);

        var stock = await svc.SiteStockAsync(material.Id);
        stock.Should().ContainSingle();
        stock[0].SiteId.Should().Be(site.Id);
        stock[0].Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Seeded_material_codes_are_preserved_by_the_taxonomy_migration()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // codes referenced by existing tests and by any real transaction history
        foreach (var code in new[] { "MAT-CEM-OPC", "MAT-STL-TMT", "MAT-ELC-WIRE", "MAT-FLR-VIT" })
            (await db.Materials.AnyAsync(m => m.Code == code)).Should().BeTrue($"{code} must survive the remap");

        // and they must sit under an ACTIVE subcategory of the new tree
        var orphaned = await db.Materials
            .CountAsync(m => !m.Subcategory.IsActive || !m.Subcategory.Category.IsActive);
        orphaned.Should().Be(0);
    }

    // ---- permissions -----------------------------------------------------
    // Every Material Master write is gated by [RequiresPermission(Permissions.MastersManage)] on
    // MaterialsController, so the role -> permission map is what actually decides who may modify.

    [Fact]
    public void Owner_may_manage_materials()
        => Permissions.ForRole(UserRole.Owner).Should().Contain(Permissions.MastersManage);

    [Fact]
    public void Supervisor_may_view_but_not_modify_materials()
    {
        var perms = Permissions.ForRole(UserRole.Supervisor);
        perms.Should().NotContain(Permissions.MastersManage);
        perms.Should().Contain(Permissions.InventoryView);
    }

    [Fact]
    public void Accountant_may_view_but_not_modify_materials()
    {
        var perms = Permissions.ForRole(UserRole.Accountant);
        perms.Should().NotContain(Permissions.MastersManage);
        perms.Should().Contain(Permissions.InventoryView);
    }

    // ---- shared arrangement ---------------------------------------------

    /// <summary>Posts a real purchase so the material genuinely carries stock and history.</summary>
    private static async Task<(Site Site, Project Project, Material Material)> SeedPurchaseAsync(
        IServiceProvider sp, AppDbContext db, decimal qty, decimal rate)
    {
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.AddRange(site, project, supplier);
        await db.SaveChangesAsync();

        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, site.Id, null, null, null, DateOnly.FromDateTime(DateTime.UtcNow), 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, qty, rate, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        return (site, project, material);
    }
}
