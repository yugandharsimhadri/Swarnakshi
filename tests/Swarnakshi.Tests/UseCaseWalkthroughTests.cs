using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Customers;
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
/// The six journeys the business named, each written the way it was described rather than the way
/// the code is layered. If one of these breaks, something a builder does every day has broken.
///
///   1. Move cement from the store to a villa
///   2. Buy cement straight for a villa (into stock, out to the villa)
///   3. Add cement to the store
///   4. Approval gates every purchase and every stock movement
///   5. Customer payments
///   6. Data entry is simple, and carries remarks
/// </summary>
public class UseCaseWalkthroughTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid SiteId, Guid ProjectId, Guid CustomerId, Guid SupplierId, Material Cement);

    private static async Task<Yard> ArrangeAsync(IServiceProvider sp, AppDbContext db, decimal saleValue = 8_000_000)
    {
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();

        var customer = new Customer { Code = "CUST-1", Name = "Ramesh Kumar", Mobile = "9000000001" };
        var supplier = new Supplier { Code = "SUP-1", Name = "Sri Balaji Traders" };
        db.AddRange(customer, supplier);
        await db.SaveChangesAsync();

        var site = await sites.CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, "Hyderabad", "Telangana", null, null, null, SiteStatus.Active, null));
        var project = await projects.CreateAsync(new SaveProjectRequest(
            "GV-101", "Villa 101", "101", site.Id, customer.Id, null, null, null, null, null,
            5_000_000, saleValue, ProjectStatus.Active, 0, null));

        var cement = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return new Yard(site.Id, project.Id, customer.Id, supplier.Id, cement);
    }

    private static async Task<PurchaseDto> BuyAsync(IServiceProvider sp, Yard y, decimal qty, decimal rate,
        Guid? deliverTo = null, string? remarks = null)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, y.SiteId, null, "INV-1", null, Today, 0, remarks,
            [new PurchaseItemInput(y.Cement.Id, y.Cement.UnitId, qty, rate, 0, 0, deliverTo)]));
        return await purchases.SubmitAsync(created.Id);
    }

    private static async Task ApproveAsync(IServiceProvider sp, string entityType, string txnRef)
    {
        var approvals = sp.GetRequiredService<IApprovalService>();
        var pending = await approvals.ListAsync(new PageQuery { PageSize = 100 }, entityType, true);
        var item = pending.Items.Single(a => a.EntityRef == txnRef);
        await approvals.DecideAsync(item.Id, new ApprovalDecision(true, "approved", false));
    }

    // ── Use case 3: add cement bags to inventory ─────────────────────────

    [Fact]
    public async Task UseCase3_Buying_cement_into_the_store_raises_stock_and_costs_no_project_anything()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 100, 400, remarks: "Lorry AP09 XX 1234, received by store keeper");

        var stock = (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single();
        stock.Quantity.Should().Be(100);
        stock.AverageRate.Should().Be(400);
        stock.Value.Should().Be(40_000);

        (await projects.SummaryAsync(y.ProjectId)).MaterialCost.Should().Be(0,
            "buying material is not spending it — that is the whole no-double-counting rule");
    }

    [Fact]
    public async Task UseCase3_A_second_delivery_at_a_different_rate_blends_into_a_weighted_average()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 100, 400);
        await BuyAsync(sp, y, 100, 450);

        var stock = (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single();
        stock.Quantity.Should().Be(200);
        stock.AverageRate.Should().Be(425);
        stock.Value.Should().Be(85_000);
    }

    // ── Use case 1: move cement from the store to a villa ────────────────

    [Fact]
    public async Task UseCase1_Cement_moves_from_the_store_to_a_villa_and_becomes_that_villas_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 100, 400);
        await BuyAsync(sp, y, 100, 450);   // store: 200 @ ₹425

        // The supervisor asks for 50 bags for the slab, and says so.
        var request = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.FromStock, Today, "For the first-floor slab",
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 50, null, null)]));
        request.Notes.Should().Be("For the first-floor slab", "the reason travels with the request");

        await requests.SubmitAsync(request.Id);
        await ApproveAsync(sp, ApprovalEntityTypes.MaterialRequest, request.TxnNumber);
        var issued = await requests.IssueAsync(request.Id, new IssueRequest(null));

        issued.RequestStatus.Should().Be(MaterialRequestStatus.Issued);
        issued.Items.Single().IssuedQty.Should().Be(50);

        var stock = (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single();
        stock.Quantity.Should().Be(150, "50 bags left the store");
        stock.Value.Should().Be(63_750);

        var summary = await projects.SummaryAsync(y.ProjectId);
        summary.MaterialCost.Should().Be(21_250, "50 bags at the store's weighted average of ₹425");
        (summary.MaterialCost + stock.Value).Should().Be(85_000, "purchased = consumed + on hand");
    }

    [Fact]
    public async Task UseCase1_The_store_cannot_issue_more_than_it_holds()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 10, 400);

        var request = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.FromStock, Today, "more than we have",
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 500, null, null)]));
        await requests.SubmitAsync(request.Id);
        await ApproveAsync(sp, ApprovalEntityTypes.MaterialRequest, request.TxnNumber);

        var act = () => requests.IssueAsync(request.Id, new IssueRequest(null));
        await act.Should().ThrowAsync<AppException>().WithMessage("*Insufficient stock*");
    }

    // ── Use case 2: buy cement straight for a villa ──────────────────────

    [Fact]
    public async Task UseCase2_Cement_bought_for_a_villa_lands_in_the_store_and_leaves_it_in_one_step()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 200, 400);   // the store already holds stock at a different rate

        await BuyAsync(sp, y, 100, 450, deliverTo: y.ProjectId, remarks: "Unloaded at Villa 101 direct");

        var stock = (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single();
        stock.Quantity.Should().Be(200, "what came in went straight out again");
        stock.AverageRate.Should().Be(400, "earmarked material must not move everybody else's valuation");
        stock.Value.Should().Be(80_000);

        (await projects.SummaryAsync(y.ProjectId)).MaterialCost.Should().Be(45_000,
            "the villa pays what was actually paid for its cement");

        // Both halves are on the stock ledger — the trail the store keeper can follow.
        var ledger = await inventory.LedgerAsync(new PageQuery { PageSize = 50 }, y.SiteId, y.Cement.Id, null, null, null, null);
        ledger.Items.Should().Contain(t => t.Type == InventoryTransactionType.PurchaseReceipt && t.Quantity == 100);
        ledger.Items.Should().Contain(t => t.Type == InventoryTransactionType.ProjectConsumption
                                           && t.Quantity == -100 && t.ProjectName == "Villa 101");
    }

    // ── Use case 4: approval gates purchases and stock movements ─────────

    [Fact]
    public async Task UseCase4_Stock_never_moves_to_a_villa_before_the_owner_approves()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 100, 400);

        var request = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 50, null, null)]));

        // Not submitted yet — issuing must refuse.
        var beforeSubmit = () => requests.IssueAsync(request.Id, new IssueRequest(null));
        await beforeSubmit.Should().ThrowAsync<AppException>().WithMessage("*must be approved*");

        await requests.SubmitAsync(request.Id);

        // Submitted but not decided — still refuses, and the store is untouched.
        var beforeApproval = () => requests.IssueAsync(request.Id, new IssueRequest(null));
        await beforeApproval.Should().ThrowAsync<AppException>().WithMessage("*must be approved*");
        (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single().Quantity.Should().Be(100);

        await ApproveAsync(sp, ApprovalEntityTypes.MaterialRequest, request.TxnNumber);
        await requests.IssueAsync(request.Id, new IssueRequest(null));

        (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single().Quantity.Should().Be(50);
    }

    [Fact]
    public async Task UseCase4_A_rejected_request_never_moves_stock()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var y = await ArrangeAsync(sp, db);

        await BuyAsync(sp, y, 100, 400);
        var request = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 50, null, null)]));
        await requests.SubmitAsync(request.Id);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 }, ApprovalEntityTypes.MaterialRequest, true);
        await approvals.DecideAsync(pending.Items.Single().Id, new ApprovalDecision(false, "not needed yet", false));

        (await requests.GetAsync(request.Id)).RequestStatus.Should().Be(MaterialRequestStatus.Rejected);
        (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single().Quantity.Should().Be(100);

        var act = () => requests.IssueAsync(request.Id, new IssueRequest(null));
        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task UseCase4_When_purchases_need_approval_stock_waits_for_the_owner()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var y = await ArrangeAsync(sp, db);

        // The setting exists so a company can insist on it; flip it on for this yard.
        var setting = await db.Settings.FirstAsync(s => s.Key == SettingKeys.PurchaseNeedsApproval);
        setting.Value = "true";
        await db.SaveChangesAsync();

        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, y.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(y.Cement.Id, y.Cement.UnitId, 100, 400, 0, 0)]));
        var submitted = await purchases.SubmitAsync(created.Id);

        submitted.Status.Should().Be(TransactionStatus.PendingApproval);
        (await inventory.BalancesAsync(y.SiteId, null, false, null))
            .Should().BeEmpty("nothing enters the store until the purchase is approved");

        await ApproveAsync(sp, ApprovalEntityTypes.Purchase, submitted.TxnNumber);

        (await inventory.BalancesAsync(y.SiteId, null, false, null)).Single().Quantity.Should().Be(100);
    }

    // ── Use case 5: customer payments ────────────────────────────────────

    [Fact]
    public async Task UseCase5_Customer_receipts_reduce_what_the_villa_still_owes()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var receipts = sp.GetRequiredService<ICustomerPaymentService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var y = await ArrangeAsync(sp, db);

        var method = await db.PaymentMethods.FirstAsync(m => m.Name == "Bank Transfer");

        await receipts.CreateAsync(new SaveCustomerPaymentRequest(
            y.ProjectId, Today, 1_000_000, method.Id, "NEFT-8891", "First instalment"));
        await receipts.CreateAsync(new SaveCustomerPaymentRequest(
            y.ProjectId, Today, 1_500_000, method.Id, "NEFT-9021", "Second instalment"));

        var summary = await projects.SummaryAsync(y.ProjectId);
        summary.CustomerReceived.Should().Be(2_500_000);
        summary.CustomerOutstanding.Should().Be(5_500_000, "₹80L sale value less ₹25L received");

        var ledger = await receipts.LedgerAsync(y.CustomerId);
        ledger.TotalReceived.Should().Be(2_500_000);
        ledger.Outstanding.Should().Be(5_500_000);
    }

    [Fact]
    public async Task UseCase5_A_receipt_needs_a_customer_on_the_project()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var receipts = sp.GetRequiredService<ICustomerPaymentService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var sites = sp.GetRequiredService<ISiteService>();

        var site = await sites.CreateAsync(new SaveSiteRequest("S2", "Self Owned", null, null, null, null, null, null, SiteStatus.Active, null));
        var selfOwned = await projects.CreateAsync(new SaveProjectRequest(
            "SO-1", "Own House", null, site.Id, null, null, null, null, null, null, 100_000, null, ProjectStatus.Active, 0, null));
        var method = await db.PaymentMethods.FirstAsync();

        var act = () => receipts.CreateAsync(new SaveCustomerPaymentRequest(
            selfOwned.Id, Today, 50_000, method.Id, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*no customer*");
    }

    // ── Use case 6: simple entry, with remarks ───────────────────────────

    [Fact]
    public async Task UseCase6_Every_entry_in_the_daily_flow_carries_a_remark()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var receipts = sp.GetRequiredService<ICustomerPaymentService>();
        var inventory = sp.GetRequiredService<IInventoryService>();
        var y = await ArrangeAsync(sp, db);

        var purchase = await BuyAsync(sp, y, 100, 400, remarks: "Lorry AP09 XX 1234");
        (await db.PurchaseHeaders.AsNoTracking().FirstAsync(p => p.Id == purchase.Id))
            .Remarks.Should().Be("Lorry AP09 XX 1234");

        var request = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.ProjectId, MaterialRequestType.FromStock, Today, "First-floor slab",
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 10, null, null)]));
        request.Notes.Should().Be("First-floor slab");

        var method = await db.PaymentMethods.FirstAsync();
        var receipt = await receipts.CreateAsync(new SaveCustomerPaymentRequest(
            y.ProjectId, Today, 100_000, method.Id, "CHQ-4412", "Part payment, cheque handed to site office"));
        receipt.Description.Should().Be("Part payment, cheque handed to site office");

        var opening = await inventory.OpeningStockAsync(new OpeningStockRequest(
            y.SiteId, y.Cement.Id, 5, 400, Today, "Counted at handover"));
        opening.Remarks.Should().Be("Counted at handover");
    }

    [Fact]
    public async Task UseCase6_A_purchase_needs_only_supplier_site_material_quantity_and_rate()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var y = await ArrangeAsync(sp, db);

        // No invoice number, no date juggling, no tax, no remarks — the shortest honest entry.
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, y.SiteId, null, null, null, Today, 0, null,
            [new PurchaseItemInput(y.Cement.Id, y.Cement.UnitId, 100, 400, 0, 0)]));

        created.TotalAmount.Should().Be(40_000);
        (await purchases.SubmitAsync(created.Id)).Status.Should().Be(TransactionStatus.Posted);
    }
}
