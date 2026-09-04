using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
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
/// Which expense head material lands under.
///
/// <para>It used to be Miscellaneous whenever the caller named no head, which is what a delivery
/// note always looks like — so a villa's cost-by-head put cement beside sundry contractor money and
/// the split told a builder nothing. The material's own category classifies it now.</para>
/// </summary>
public class MaterialCostClassificationTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid SiteId, Guid ProjectId, Guid SupplierId);

    private static async Task<Fixture> ArrangeAsync(IServiceProvider sp, AppDbContext db)
    {
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(
            new SaveSiteRequest("GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        var project = await sp.GetRequiredService<IProjectService>().CreateAsync(
            new SaveProjectRequest("GV-101", "Villa 101", "101", site.Id, null, null, null, null, null, null,
                5_000_000, null, ProjectStatus.Active, 0, null));

        var supplier = new Supplier { Code = "SUP-1", Name = "Sri Balaji Traders" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return new Fixture(site.Id, project.Id, supplier.Id);
    }

    private static Task<Material> MaterialAsync(AppDbContext db, string code)
        => db.Materials.Include(m => m.Subcategory).ThenInclude(s => s.Category).FirstAsync(m => m.Code == code);

    private static async Task BuyDirectAsync(IServiceProvider sp, Fixture f, Material m,
        decimal qty, decimal rate)
    {
        var created = await sp.GetRequiredService<IPurchaseService>().CreateAsync(new SavePurchaseRequest(
            f.SupplierId, null, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(m.Id, m.UnitId, qty, rate, 0, 0, f.ProjectId)]));
        await sp.SubmitAndApproveAsync(created.Id);
    }

    /// <summary>The head a project expense was written under, by its description prefix.</summary>
    private static Task<string> HeadOfAsync(AppDbContext db, Guid projectId, string descriptionStartsWith)
        => db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Description!.StartsWith(descriptionStartsWith))
            .Select(e => e.Head.Name)
            .FirstAsync();

    [Fact]
    public async Task Material_delivered_straight_to_a_villa_is_filed_under_its_own_category()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var cement = await MaterialAsync(db, "MAT-CEM-OPC");
        cement.Subcategory.Category.Name.Should().Be("Civil & Structure");

        await BuyDirectAsync(sp, f, cement, 100, 450);

        (await HeadOfAsync(db, f.ProjectId, "Direct delivery"))
            .Should().Be("Civil & Structure", "the material classifies its own cost");
    }

    [Fact]
    public async Task Two_trades_land_under_two_heads_rather_than_both_under_Miscellaneous()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        var cement = await MaterialAsync(db, "MAT-CEM-OPC");
        var pipe = await MaterialAsync(db, "MAT-PLB-CPVC");
        pipe.Subcategory.Category.Name.Should().Be("Plumbing");

        await BuyDirectAsync(sp, f, cement, 100, 450);
        await BuyDirectAsync(sp, f, pipe, 50, 110);

        var heads = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == f.ProjectId && e.ExpenseType == ProjectExpenseType.Material)
            .Select(e => e.Head.Name)
            .ToListAsync();

        heads.Should().BeEquivalentTo(["Civil & Structure", "Plumbing"]);
        heads.Should().NotContain("Miscellaneous", "that was the bug this replaced");
    }

    [Fact]
    public async Task An_existing_head_of_the_same_name_is_reused_not_duplicated()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeAsync(sp, db);

        // "Plumbing" is already a seeded work stage. The category of the same name must find it
        // rather than adding a second head that splits the same trade across two rows.
        var seeded = await db.ExpenseHeads.CountAsync(h => h.Name == "Plumbing");
        seeded.Should().Be(1);

        var pipe = await MaterialAsync(db, "MAT-PLB-CPVC");
        await BuyDirectAsync(sp, f, pipe, 50, 110);

        (await db.ExpenseHeads.CountAsync(h => h.Name == "Plumbing")).Should().Be(1);
        (await HeadOfAsync(db, f.ProjectId, "Direct delivery")).Should().Be("Plumbing");
    }

    [Fact]
    public async Task A_head_chosen_on_a_material_request_still_wins_over_the_category()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var f = await ArrangeAsync(sp, db);

        var cement = await MaterialAsync(db, "MAT-CEM-OPC");
        await BuyDirectAsync(sp, f, cement, 0.0001m, 1);          // touch, so a balance row exists
        var stocked = await sp.GetRequiredService<IPurchaseService>().CreateAsync(new SavePurchaseRequest(
            f.SupplierId, null, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(cement.Id, cement.UnitId, 100, 450, 0, 0, null)]));
        await sp.SubmitAndApproveAsync(stocked.Id);

        // Someone raising a request knows the stage the material is for. That must not be overridden.
        var rcc = await db.ExpenseHeads.FirstAsync(h => h.Name == "RCC");
        var created = await requests.CreateAsync(new SaveMaterialRequestRequest(
            f.ProjectId, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(cement.Id, cement.UnitId, 40, rcc.Id, null)]));
        await requests.SubmitAsync(created.Id);
        await sp.ApproveAsync(ApprovalEntityTypes.MaterialRequest, created.Id);
        await requests.IssueAsync(created.Id, new IssueRequest(null));

        (await HeadOfAsync(db, f.ProjectId, "Consumption"))
            .Should().Be("RCC", "an explicit stage beats the material's category");
    }
}
