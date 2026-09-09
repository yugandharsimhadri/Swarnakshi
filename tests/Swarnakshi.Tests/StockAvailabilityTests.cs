using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// A villa can only be charged for material that actually left the shelf. A request the store
/// cannot fill is therefore not a request at all — it is an approval the owner will give for an
/// amount that can never become a cost, and a stock ledger and a project cost that stop agreeing.
/// </summary>
public class StockAvailabilityTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid SiteId, Guid ProjectId, Guid SupplierId, Material Cement, Material Steel);

    private static async Task<Yard> ArrangeAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Green Valley", Status = SiteStatus.Active };
        var project = new Project
        {
            Code = "P1", Name = "Villa 101", Site = site,
            EstimatedCost = 1_000_000, Status = ProjectStatus.Active
        };
        var supplier = new Supplier { Code = "SUP1", Name = "Sri Balaji Traders" };
        db.AddRange(site, project, supplier);
        await db.SaveChangesAsync();

        var cement = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        var steel = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code != "MAT-CEM-OPC");
        return new Yard(site.Id, project.Id, supplier.Id, cement, steel);
    }

    /// <summary>Buys <paramref name="qty"/> into the store and gets it approved, so it is really there.</summary>
    private static async Task StockAsync(IServiceProvider sp, Yard y, Material material, decimal qty, decimal rate = 400)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, null, y.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, qty, rate, 0, 0)]));
        await sp.SubmitAndApproveAsync(created.Id);
    }

    private static SaveMaterialRequestRequest Request(Yard y, params (Material Material, decimal Qty)[] lines) =>
        new(y.ProjectId, MaterialRequestType.FromStock, Today, null,
            lines.Select(l => new MaterialRequestItemInput(l.Material.Id, l.Material.UnitId, l.Qty, null, null)).ToList());

    [Fact]
    public async Task A_request_for_more_than_the_store_holds_is_refused_when_it_is_raised()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);
        await StockAsync(sp, y, y.Cement, 100);

        var act = () => requests.CreateAsync(Request(y, (y.Cement, 150)));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("asked 150").And.Contain("in store 100");

        (await db.MaterialRequests.CountAsync()).Should().Be(0, "the request is refused, not saved as a draft");
    }

    [Fact]
    public async Task A_request_for_a_material_the_store_has_never_held_is_refused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);

        // No balance row exists at all for this material on this site — the "in store 0" case.
        var act = () => requests.CreateAsync(Request(y, (y.Cement, 1)));

        (await act.Should().ThrowAsync<AppException>()).Which.Message.Should().Contain("in store 0");
    }

    [Fact]
    public async Task Two_lines_of_the_same_material_are_added_up_before_being_checked()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);
        await StockAsync(sp, y, y.Cement, 100);

        // Each line fits inside the balance; together they do not. A per-line check waves this
        // through, and a long request written stage by stage is exactly how it happens.
        var act = () => requests.CreateAsync(Request(y, (y.Cement, 60), (y.Cement, 60)));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("asked 120").And.Contain("in store 100");
    }

    [Fact]
    public async Task Every_short_material_is_named_not_just_the_first()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);
        await StockAsync(sp, y, y.Cement, 10);
        await StockAsync(sp, y, y.Steel, 5);

        var act = () => requests.CreateAsync(Request(y, (y.Cement, 50), (y.Steel, 50)));

        // Fixing one and being told about the next is two round trips for one mistake.
        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain(y.Cement.Name).And.Contain(y.Steel.Name);
    }

    [Fact]
    public async Task A_request_the_store_can_cover_goes_through_and_still_issues()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var inventory = sp.GetRequiredService<Application.Inventory.IInventoryService>();
        var y = await ArrangeAsync(db);
        await StockAsync(sp, y, y.Cement, 100);

        var req = await requests.CreateAsync(Request(y, (y.Cement, 40)));
        await requests.SubmitAsync(req.Id);
        await sp.ApproveAsync(Application.Approvals.ApprovalEntityTypes.MaterialRequest, req.Id);
        await requests.IssueAsync(req.Id, new IssueRequest(null));

        (await inventory.BalancesAsync(y.SiteId, null, false, null))
            .Single(b => b.MaterialId == y.Cement.Id).Quantity.Should().Be(60);
    }

    [Fact]
    public async Task A_draft_written_before_the_store_emptied_cannot_be_submitted()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);
        await StockAsync(sp, y, y.Cement, 100);

        // Raised while the cement was there.
        var draft = await requests.CreateAsync(Request(y, (y.Cement, 80)));

        // Another villa takes most of it in the meantime.
        var other = await requests.CreateAsync(Request(y, (y.Cement, 70)));
        await requests.SubmitAsync(other.Id);
        await sp.ApproveAsync(Application.Approvals.ApprovalEntityTypes.MaterialRequest, other.Id);
        await requests.IssueAsync(other.Id, new IssueRequest(null));

        // Sending this to the owner now would spend their attention on something nobody can issue.
        var act = () => requests.SubmitAsync(draft.Id);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("asked 80").And.Contain("in store 30");
    }

    [Fact]
    public async Task A_purchase_request_is_not_checked_against_the_store()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(db);

        // Asking to BUY material is the correct response to an empty store, so the check that
        // refuses an empty store must not also refuse the way out of it.
        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.Purchase, Today, "None in store — buy it",
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 500, null, null)]));

        req.RequestStatus.Should().Be(MaterialRequestStatus.Draft);
    }
}
