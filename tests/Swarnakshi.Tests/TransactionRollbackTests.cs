using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
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
/// A posting either happens completely or not at all.
///
/// <para>These are the paths where one action writes to several tables at once — a stock movement,
/// a valuation, a project cost row, a status change. Half of that is worse than none of it: stock
/// that left the store without being charged to a villa is a discrepancy nobody can explain later,
/// and reconciliation depends on the two sides never disagreeing.</para>
///
/// <para>Each test forces a failure partway through a multi-row posting and then checks that
/// nothing at all was written — not that an error was returned.</para>
/// </summary>
public class TransactionRollbackTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Fixture(Guid SiteId, Guid ProjectId, Guid SupplierId, Material Cement, Material Steel);

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

        var cement = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");
        var steel = await db.Materials.FirstAsync(m => m.Code == "MAT-STL-TMT");
        return new Fixture(site.Id, project.Id, supplier.Id, cement, steel);
    }

    /// <summary>Buys stock so there is something to issue, and something to fall short of.</summary>
    private static async Task StockAsync(IServiceProvider sp, Fixture f, Material m, decimal qty, decimal rate)
    {
        var created = await sp.GetRequiredService<IPurchaseService>().CreateAsync(new SavePurchaseRequest(
            f.SupplierId, null, f.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(m.Id, m.UnitId, qty, rate, 0, 0, null)]));
        await sp.SubmitAndApproveAsync(created.Id);
    }

    [Fact]
    public async Task An_issue_that_runs_out_of_stock_halfway_writes_nothing_at_all()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var f = await ArrangeAsync(sp, db);

        // Plenty of cement, barely any steel.
        await StockAsync(sp, f, f.Cement, 500, 400);
        await StockAsync(sp, f, f.Steel, 10, 60);

        var cementBefore = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.MaterialId == f.Cement.Id).Select(b => b.Quantity).SingleAsync();
        var costRowsBefore = await db.ProjectExpenses.CountAsync(e => e.ProjectId == f.ProjectId);
        var ledgerBefore = await db.InventoryTransactions.CountAsync();

        // Cement first, then more steel than exists. The cement line issues cleanly; the steel
        // line throws. That is the shape of the failure that matters — not a request rejected up
        // front, but one that fails after it has already moved something.
        var created = await requests.CreateAsync(new SaveMaterialRequestRequest(
            f.ProjectId, MaterialRequestType.FromStock, Today, null,
            [
                new MaterialRequestItemInput(f.Cement.Id, f.Cement.UnitId, 100, null, null),
                new MaterialRequestItemInput(f.Steel.Id, f.Steel.UnitId, 5_000, null, null),
            ]));
        await requests.SubmitAsync(created.Id);
        await sp.ApproveAsync(ApprovalEntityTypes.MaterialRequest, created.Id);

        var act = () => requests.IssueAsync(created.Id, new IssueRequest(null));
        await act.Should().ThrowAsync<AppException>().WithMessage("*Insufficient stock*");

        // A fresh context, because the failed one still holds the tracked changes in memory. What
        // matters is what reached the database.
        await using var check = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestDatabase.ConnectionString).Options,
            host.CurrentUser);

        using var tenant = check.BeginTenantScope(host.CompanyId);
        (await check.InventoryBalances.AsNoTracking()
                .Where(b => b.MaterialId == f.Cement.Id).Select(b => b.Quantity).SingleAsync())
            .Should().Be(cementBefore, "the cement line was issued before the steel line failed");
        (await check.ProjectExpenses.CountAsync(e => e.ProjectId == f.ProjectId))
            .Should().Be(costRowsBefore, "no cost may be charged for material that never moved");
        (await check.InventoryTransactions.CountAsync())
            .Should().Be(ledgerBefore, "the ledger must not record a movement that was undone");
    }

    [Fact]
    public async Task The_request_is_still_approved_and_can_be_issued_once_the_stock_is_there()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var f = await ArrangeAsync(sp, db);

        await StockAsync(sp, f, f.Cement, 500, 400);
        await StockAsync(sp, f, f.Steel, 10, 60);

        var created = await requests.CreateAsync(new SaveMaterialRequestRequest(
            f.ProjectId, MaterialRequestType.FromStock, Today, null,
            [
                new MaterialRequestItemInput(f.Cement.Id, f.Cement.UnitId, 100, null, null),
                new MaterialRequestItemInput(f.Steel.Id, f.Steel.UnitId, 5_000, null, null),
            ]));
        await requests.SubmitAsync(created.Id);
        await sp.ApproveAsync(ApprovalEntityTypes.MaterialRequest, created.Id);

        await ((Func<Task>)(() => requests.IssueAsync(created.Id, new IssueRequest(null))))
            .Should().ThrowAsync<AppException>();

        // Rolling back the movement must not also cancel the request. The storekeeper buys the
        // steel and issues the same request again; if the rollback had left it Issued or Cancelled
        // they would have to raise it from scratch.
        await StockAsync(sp, f, f.Steel, 6_000, 62);
        await requests.IssueAsync(created.Id, new IssueRequest(null));

        await using var check = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestDatabase.ConnectionString).Options,
            host.CurrentUser);
        using var tenant = check.BeginTenantScope(host.CompanyId);

        (await check.ProjectExpenses.CountAsync(e => e.ProjectId == f.ProjectId))
            .Should().Be(2, "both lines are charged, once, on the successful attempt");
    }
}
