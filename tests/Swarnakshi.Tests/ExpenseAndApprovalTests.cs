using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Projects;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Direct project expenses, the simple-master delete guard, and the approval REJECT path —
/// none of which were previously covered.
/// </summary>
public class ExpenseAndApprovalTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<(Site Site, Project Project)> ArrangeAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project
        {
            Code = "P1", Name = "Villa 1", Site = site, EstimatedCost = 1_000_000,
            ContractSaleValue = 2_000_000, Status = ProjectStatus.Active
        };
        db.AddRange(site, project);
        await db.SaveChangesAsync();
        return (site, project);
    }

    // ---- direct project expenses ----------------------------------------

    [Fact]
    public async Task A_direct_expense_posts_straight_into_project_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var (_, project) = await ArrangeAsync(db);
        var head = await db.ExpenseHeads.FirstAsync();

        var e = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            project.Id, Today, head.Id, null, "Site fencing", 25_000m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));

        e.Status.Should().Be(TransactionStatus.Posted);
        e.TxnNumber.Should().NotBeNullOrWhiteSpace();

        var summary = await projects.SummaryAsync(project.Id);
        summary.OtherCost.Should().Be(25_000m);
        summary.TotalCost.Should().Be(25_000m);
    }

    [Fact]
    public async Task Cancelling_an_expense_removes_it_from_project_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var (_, project) = await ArrangeAsync(db);
        var head = await db.ExpenseHeads.FirstAsync();

        var e = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            project.Id, Today, head.Id, null, "Booked twice by mistake", 40_000m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));
        (await projects.SummaryAsync(project.Id)).TotalCost.Should().Be(40_000m);

        await expenses.CancelAsync(e.Id, "duplicate entry");

        (await projects.SummaryAsync(project.Id)).TotalCost.Should().Be(0,
            "a cancelled expense must stop counting toward project cost");

        // …but the row itself survives for the audit trail.
        (await db.ProjectExpenses.AsNoTracking().CountAsync(x => x.Id == e.Id)).Should().Be(1);
    }

    [Fact]
    public async Task An_expense_subhead_must_belong_to_its_head()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var (_, project) = await ArrangeAsync(db);

        var head = await db.ExpenseHeads.FirstAsync();
        var foreignSubhead = await db.ExpenseSubheads.FirstAsync(s => s.ExpenseHeadId != head.Id);

        var act = () => expenses.CreateAsync(new SaveProjectExpenseRequest(
            project.Id, Today, head.Id, foreignSubhead.Id, "Mismatched", 1_000m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task Expenses_for_an_unknown_project_are_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var head = await db.ExpenseHeads.FirstAsync();

        var act = () => expenses.CreateAsync(new SaveProjectExpenseRequest(
            Guid.NewGuid(), Today, head.Id, null, "Orphan", 500m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Cost_by_head_groups_expenses_for_the_project()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var (_, project) = await ArrangeAsync(db);
        var head = await db.ExpenseHeads.FirstAsync();

        await expenses.CreateAsync(new SaveProjectExpenseRequest(project.Id, Today, head.Id, null, "A",
            10_000m, ProjectExpenseType.Direct, PaymentStatus.Paid, null));
        await expenses.CreateAsync(new SaveProjectExpenseRequest(project.Id, Today, head.Id, null, "B",
            15_000m, ProjectExpenseType.Direct, PaymentStatus.Paid, null));

        var byHead = await expenses.CostByHeadAsync(project.Id);

        byHead.Single(h => h.ExpenseHeadId == head.Id).Amount.Should().Be(25_000m);
    }

    // ---- approval: the REJECT path --------------------------------------

    [Fact]
    public async Task Rejecting_a_material_request_leaves_stock_and_project_cost_untouched()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var (site, project) = await ArrangeAsync(db);

        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        var material = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, site.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 100, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            project.Id, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(material.Id, material.UnitId, 30, null, null)]));
        await requests.SubmitAsync(req.Id);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 },
            ApprovalEntityTypes.MaterialRequest, true);
        await approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(false, "not needed yet", false));

        // stock is untouched and nothing was booked to the project
        var balance = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.MaterialId == material.Id);
        balance.Quantity.Should().Be(100);
        balance.Value.Should().Be(40_000);
        (await projects.SummaryAsync(project.Id)).MaterialCost.Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_request_cannot_then_be_issued()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var (site, project) = await ArrangeAsync(db);

        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var pur = await purchases.CreateAsync(new SavePurchaseRequest(
            supplier.Id, null, site.Id, null, null, null, Today, 0, null,
            [new PurchaseItemInput(material.Id, material.UnitId, 50, 400, 0, 0)]));
        await sp.SubmitAndApproveAsync(pur.Id);

        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            project.Id, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(material.Id, material.UnitId, 10, null, null)]));
        await requests.SubmitAsync(req.Id);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 },
            ApprovalEntityTypes.MaterialRequest, true);
        await approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(false, "rejected", false));

        var act = () => requests.IssueAsync(req.Id, new IssueRequest(null));

        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Approval_history_records_the_decision_and_the_pending_count_drops()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var (_, project) = await ArrangeAsync(db);
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            project.Id, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(material.Id, material.UnitId, 5, null, null)]));
        await requests.SubmitAsync(req.Id);

        (await approvals.PendingCountAsync()).Should().Be(1);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 },
            ApprovalEntityTypes.MaterialRequest, true);
        var approvalId = pending.Items[0].Id;
        await approvals.DecideAsync(approvalId, new ApprovalDecision(false, "insufficient justification", false));

        (await approvals.PendingCountAsync()).Should().Be(0);

        var history = await approvals.HistoryAsync(approvalId);
        history.Should().NotBeEmpty();
        history.Should().Contain(h => h.Remarks != null && h.Remarks.Contains("insufficient justification"));
    }

    // ---- simple-master delete guard --------------------------------------

    [Fact]
    public async Task A_referenced_master_row_cannot_be_deleted()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var simple = sp.GetRequiredService<ISimpleMasterService>();

        // Every seeded material points at a unit, so the unit is in use.
        var unitInUse = await db.Materials.Select(m => m.UnitId).FirstAsync();

        var act = () => simple.DeleteAsync(SimpleMasterKind.Unit, unitInUse);

        await act.Should().ThrowAsync<AppException>().WithMessage("*in use*");
        (await db.Units.AnyAsync(u => u.Id == unitInUse)).Should().BeTrue();
    }

    [Fact]
    public async Task An_unused_master_row_can_be_created_and_deleted()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var simple = sp.GetRequiredService<ISimpleMasterService>();

        var id = await simple.SaveAsync(SimpleMasterKind.Unit,
            null, new SaveSimpleMasterRequest("Test Unit", "TSTU", null, 99, true));
        (await db.Units.AnyAsync(u => u.Id == id)).Should().BeTrue();

        await simple.DeleteAsync(SimpleMasterKind.Unit, id);

        (await db.Units.AnyAsync(u => u.Id == id)).Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_unit_codes_are_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var simple = scope.ServiceProvider.GetRequiredService<ISimpleMasterService>();

        await simple.SaveAsync(SimpleMasterKind.Unit, null,
            new SaveSimpleMasterRequest("Dup Unit", "DUPE", null, 1, true));
        var act = () => simple.SaveAsync(SimpleMasterKind.Unit, null,
            new SaveSimpleMasterRequest("Other", "DUPE", null, 2, true));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task A_master_row_requires_a_name()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var simple = scope.ServiceProvider.GetRequiredService<ISimpleMasterService>();

        var act = () => simple.SaveAsync(SimpleMasterKind.LabourCategory, null,
            new SaveSimpleMasterRequest("  ", null, null, 1, true));

        await act.Should().ThrowAsync<AppException>().WithMessage("*required*");
    }
}
