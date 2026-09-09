using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Customers;
using Swarnakshi.Application.Employees;
using Swarnakshi.Application.Expenses;
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
/// One villa, from an empty site to a half-built house with money owed both ways, driven entirely
/// through the services a person actually uses — and then every number checked against every other.
///
/// <para>The rest of the suite tests each posting on its own. This is the one that would catch a
/// pair of them disagreeing: material charged twice, stock that left the store without landing on a
/// villa, a total that is not the sum of its parts. Those are the failures nobody notices for a
/// month and then cannot reconstruct.</para>
/// </summary>
public class EndToEndVillaTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(
        Guid SiteId, Guid VillaId, Guid CustomerId, Guid SupplierId, Guid ContractorId,
        Guid EmployeeId, Guid PaymentMethodId, Guid ExpenseHeadId, Guid LabourCategoryId,
        Material Cement, Material Steel);

    private static async Task<Yard> ArrangeAsync(IServiceProvider sp, AppDbContext db)
    {
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, "Hyderabad", "Telangana", null, null, null, SiteStatus.Active, null));

        var customer = new Customer { Code = "CUS-1", Name = "Ramesh Kumar", Mobile = "9000000001" };
        var supplier = new Supplier { Code = "SUP-1", Name = "Sri Balaji Traders" };
        var contractor = new Contractor { Code = "CON-1", Name = "Ravi Masonry", IsActive = true };
        db.AddRange(customer, supplier, contractor);
        await db.SaveChangesAsync();

        var villa = await sp.GetRequiredService<IProjectService>().CreateAsync(new SaveProjectRequest(
            "GV-101", "Villa 101", "101", site.Id, customer.Id, null, null, null, null, null,
            EstimatedCost, SaleValue, ProjectStatus.Active, 50, null));

        var employee = await sp.GetRequiredService<IEmployeeService>().CreateAsync(new SaveEmployeeRequest(
            null, "Anil Site Engineer", "9000000002", 20_000, Today, null, "Engineer", null, null, site.Id, true));

        return new Yard(
            site.Id, villa.Id, customer.Id, supplier.Id, contractor.Id, employee.Id,
            await db.PaymentMethods.Select(m => m.Id).FirstAsync(),
            await db.ExpenseHeads.Select(h => h.Id).FirstAsync(),
            await db.LabourCategories.Select(c => c.Id).FirstAsync(),
            await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC"),
            await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-STL-TMT"));
    }

    private const decimal EstimatedCost = 5_000_000m;
    private const decimal SaleValue = 8_000_000m;

    // ---- the acts, each ending in the state a person would see -------------

    /// <summary>Buys into the store. Inventory value, and nobody's cost yet.</summary>
    private static async Task BuyIntoStoreAsync(IServiceProvider sp, Yard y, Material m, decimal qty, decimal rate)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, null, y.SiteId, null, "INV-1", null, Today, 0, null,
            [new PurchaseItemInput(m.Id, m.UnitId, qty, rate, 0, 0)]));
        await sp.SubmitAndApproveAsync(created.Id);
    }

    /// <summary>Buys straight for the villa: in through the store and out again, in one posting.</summary>
    private static async Task<PurchaseDto> BuyForVillaAsync(IServiceProvider sp, Yard y, Material m, decimal qty, decimal rate)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, null, y.SiteId, null, "INV-2", null, Today, 0, null,
            [new PurchaseItemInput(m.Id, m.UnitId, qty, rate, 0, 0, y.VillaId)]));
        return await sp.SubmitAndApproveAsync(created.Id);
    }

    /// <summary>Request, owner approval, issue — the three acts that turn stock into a villa's cost.</summary>
    private static async Task IssueToVillaAsync(IServiceProvider sp, Yard y, Material m, decimal qty)
    {
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            y.VillaId, MaterialRequestType.FromStock, Today, "Slab",
            [new MaterialRequestItemInput(m.Id, m.UnitId, qty, null, null)]));
        await requests.SubmitAsync(req.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.MaterialRequest, req.Id);
        await requests.IssueAsync(req.Id, new IssueRequest(null));
    }

    // ---- the whole life of a villa -----------------------------------------

    [Fact]
    public async Task Every_number_on_the_villa_agrees_with_every_other_after_a_full_month_of_work()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var y = await ArrangeAsync(sp, db);

        // 1. Two lorries into the store: 500 cement at 400 = 200,000, and 100 steel at 600 = 60,000.
        await BuyIntoStoreAsync(sp, y, y.Cement, 500, 400);
        await BuyIntoStoreAsync(sp, y, y.Steel, 100, 600);

        var afterBuying = await sp.GetRequiredService<IProjectService>().SummaryAsync(y.VillaId);
        afterBuying.TotalCost.Should().Be(0,
            "material in the store is inventory value, not any villa's cost — this is the invariant "
            + "the whole product rests on, and the point at which double counting would start");

        // 2. 200 cement out to the villa: 80,000 of cost, and the store drops by exactly that.
        await IssueToVillaAsync(sp, y, y.Cement, 200);

        // 3. 50 cement bought straight for the villa at a different rate: 50 x 420 = 21,000. It goes
        //    in and out of the store at its own landed rate, so the pool's average is untouched.
        await BuyForVillaAsync(sp, y, y.Cement, 50, 420);

        // 4. Contractor: a 300,000 work order, 100,000 paid against it.
        var work = await sp.GetRequiredService<IContractWorkService>().CreateAsync(new SaveContractWorkRequest(
            y.VillaId, y.ContractorId, "Masonry", "Ground floor", 300_000, Today, null, null, ContractWorkStatus.Active));
        var contractorPayments = sp.GetRequiredService<IContractorPaymentService>();
        var paid = await contractorPayments.CreateAsync(new SaveContractorPaymentRequest(
            y.ContractorId, y.VillaId, work.Id, Today, 100_000, y.PaymentMethodId, "NEFT/1", null,
            ContractorPaymentKind.Partial));
        await contractorPayments.SubmitAsync(paid.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.ContractorPayment, paid.Id);

        // 5. Day labour: 25,000.
        var labour = sp.GetRequiredService<ILabourService>();
        var entry = await labour.CreateAsync(new SaveLabourEntryRequest(
            y.VillaId, y.LabourCategoryId, LabourPeriodType.Weekly, Today, Today, 25_000, null, "Weekly", null));
        await labour.SubmitAsync(entry.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.LabourEntry, entry.Id);

        // 6. The site engineer's salary, charged to this villa: 20,000. Labour, not "other".
        var employeePayments = sp.GetRequiredService<IEmployeePaymentService>();
        var salary = await employeePayments.CreateAsync(new SaveEmployeePaymentRequest(
            y.EmployeeId, Today, EmployeePaymentKind.Salary, 20_000, 0, null, null,
            y.PaymentMethodId, null, y.VillaId, null));
        await employeePayments.SubmitAsync(salary.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.EmployeePayment, salary.Id);

        // 7. A direct expense: 15,000 of scaffolding hire.
        var expense = await sp.GetRequiredService<IProjectExpenseService>().CreateAsync(
            new SaveProjectExpenseRequest(y.VillaId, Today, y.ExpenseHeadId, null, "Scaffolding hire",
                15_000, ProjectExpenseType.Direct, PaymentStatus.Paid, y.PaymentMethodId));
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.ProjectExpense, expense.Id);

        // 8. The customer pays two instalments: 3,000,000.
        var receipts = sp.GetRequiredService<ICustomerPaymentService>();
        foreach (var amount in new[] { 2_000_000m, 1_000_000m })
        {
            var receipt = await receipts.CreateAsync(new SaveCustomerPaymentRequest(
                y.VillaId, Today, amount, y.PaymentMethodId, "NEFT", null));
            await sp.ApproveIfPendingAsync(ApprovalEntityTypes.CustomerPayment, receipt.Id);
        }

        // ---- and now the books ------------------------------------------------
        var summary = await sp.GetRequiredService<IProjectService>().SummaryAsync(y.VillaId);

        summary.MaterialCost.Should().Be(101_000m, "80,000 issued from the store plus 21,000 delivered straight to the villa");
        summary.LabourCost.Should().Be(45_000m, "25,000 of day labour plus the engineer's 20,000 salary");
        summary.ContractorCost.Should().Be(100_000m, "what has actually been paid, not what was promised");
        summary.OtherCost.Should().Be(15_000m, "the scaffolding");

        summary.TotalCost.Should().Be(
            summary.MaterialCost + summary.LabourCost + summary.ContractorCost + summary.OtherCost,
            "the total is the sum of its parts and nothing else — a difference here is money nobody can explain");
        summary.TotalCost.Should().Be(261_000m);

        // Promised but not yet paid is shown beside the spend, never inside it: nothing has left
        // the bank for the remaining 200,000 of the work order.
        summary.CommittedContractorCost.Should().Be(200_000m);
        summary.CommittedTotalCost.Should().Be(461_000m);

        summary.CustomerReceived.Should().Be(3_000_000m);
        summary.CustomerOutstanding.Should().Be(SaleValue - 3_000_000m);
        summary.BudgetVariance.Should().Be(EstimatedCost - 261_000m);

        // What is left on the shelf: 300 cement at 400 and 100 steel at 600. The direct-delivery
        // purchase passed straight through, so it must have left the pool exactly as it entered.
        var balances = await sp.GetRequiredService<IInventoryService>().BalancesAsync(y.SiteId, null, false, null);
        var cement = balances.Single(b => b.MaterialId == y.Cement.Id);
        cement.Quantity.Should().Be(300m);
        cement.AverageRate.Should().Be(400m, "material earmarked for one villa must not move the pool's valuation");
        balances.Sum(b => b.Value).Should().Be(180_000m);
    }

    [Fact]
    public async Task Every_rupee_of_material_bought_is_either_on_the_shelf_or_on_a_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var y = await ArrangeAsync(sp, db);

        await BuyIntoStoreAsync(sp, y, y.Cement, 500, 400);      // 200,000
        await BuyIntoStoreAsync(sp, y, y.Steel, 100, 600);       //  60,000
        await IssueToVillaAsync(sp, y, y.Cement, 200);           //  80,000 to the villa
        await IssueToVillaAsync(sp, y, y.Steel, 40);             //  24,000 to the villa
        await BuyForVillaAsync(sp, y, y.Cement, 50, 420);        //  21,000 in and straight out

        var purchased = await db.InventoryTransactions.AsNoTracking()
            .Where(t => t.Type == InventoryTransactionType.PurchaseReceipt)
            .SumAsync(t => t.Amount);

        var onTheShelf = (await sp.GetRequiredService<IInventoryService>()
            .BalancesAsync(y.SiteId, null, false, null)).Sum(b => b.Value);

        var onVillas = (await sp.GetRequiredService<IProjectService>().SummaryAsync(y.VillaId)).MaterialCost;

        // The whole reconciliation in one line. If it ever fails, material has either been charged
        // twice or has left the store without being charged to anybody — and both are the kind of
        // discrepancy that is impossible to unpick a month later.
        (onTheShelf + onVillas).Should().Be(purchased,
            $"bought {purchased:N0} = {onTheShelf:N0} still in the store + {onVillas:N0} charged to villas");
    }

    [Fact]
    public async Task Nothing_that_is_still_waiting_for_the_owner_counts_towards_anything()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var y = await ArrangeAsync(sp, db);
        await BuyIntoStoreAsync(sp, y, y.Cement, 500, 400);

        // Every kind of posting raised and left in the queue. Not one is approved.
        await sp.GetRequiredService<IProjectExpenseService>().CreateAsync(new SaveProjectExpenseRequest(
            y.VillaId, Today, y.ExpenseHeadId, null, "Unapproved", 50_000,
            ProjectExpenseType.Direct, PaymentStatus.Paid, y.PaymentMethodId));

        var receipt = await sp.GetRequiredService<ICustomerPaymentService>().CreateAsync(
            new SaveCustomerPaymentRequest(y.VillaId, Today, 500_000, y.PaymentMethodId, null, null));

        var labour = sp.GetRequiredService<ILabourService>();
        var entry = await labour.CreateAsync(new SaveLabourEntryRequest(
            y.VillaId, y.LabourCategoryId, LabourPeriodType.Weekly, Today, Today, 30_000, null, "Weekly", null));
        await labour.SubmitAsync(entry.Id);

        var summary = await sp.GetRequiredService<IProjectService>().SummaryAsync(y.VillaId);

        summary.TotalCost.Should().Be(0, "an expense nobody has agreed to is not yet a cost");
        summary.CustomerReceived.Should().Be(0, "a receipt nobody has agreed to has not reduced what is owed");
        summary.CustomerOutstanding.Should().Be(SaleValue);

        // …and the queue is holding exactly those three.
        (await sp.GetRequiredService<IApprovalService>().PendingCountAsync()).Should().Be(3);
        receipt.Status.Should().Be(TransactionStatus.PendingApproval);
    }

    [Fact]
    public async Task The_auto_approve_limit_lets_the_small_ones_through_and_stops_the_rest()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var y = await ArrangeAsync(sp, db);

        await sp.SetAutoApproveLimitAsync(20_000m);

        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var small = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            y.VillaId, Today, y.ExpenseHeadId, null, "Tea and cement blocks", 8_000,
            ProjectExpenseType.Direct, PaymentStatus.Paid, y.PaymentMethodId));
        var large = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            y.VillaId, Today, y.ExpenseHeadId, null, "Crane hire", 90_000,
            ProjectExpenseType.Direct, PaymentStatus.Paid, y.PaymentMethodId));

        small.Status.Should().Be(TransactionStatus.Posted);
        large.Status.Should().Be(TransactionStatus.PendingApproval);

        // A purchase of 12,000 is under the limit too, so the stock arrives without anybody asked.
        await BuyIntoStoreAsync(sp, y, y.Cement, 30, 400);
        (await sp.GetRequiredService<IInventoryService>().BalancesAsync(y.SiteId, null, false, null))
            .Single(b => b.MaterialId == y.Cement.Id).Quantity.Should().Be(30);

        var summary = await sp.GetRequiredService<IProjectService>().SummaryAsync(y.VillaId);
        summary.TotalCost.Should().Be(8_000m, "only the small expense has posted");
        (await sp.GetRequiredService<IApprovalService>().PendingCountAsync()).Should().Be(1, "the crane hire");
    }

    [Fact]
    public async Task A_villas_cost_never_includes_what_was_spent_on_another_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var projects = sp.GetRequiredService<IProjectService>();
        var y = await ArrangeAsync(sp, db);

        var neighbour = await projects.CreateAsync(new SaveProjectRequest(
            "GV-102", "Villa 102", "102", y.SiteId, null, null, null, null, null, null,
            EstimatedCost, null, ProjectStatus.Active, 20, null));

        await BuyIntoStoreAsync(sp, y, y.Cement, 500, 400);
        await IssueToVillaAsync(sp, y, y.Cement, 100);   // 40,000 to Villa 101

        // The same store, the same material, a different villa.
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            neighbour.Id, MaterialRequestType.FromStock, Today, null,
            [new MaterialRequestItemInput(y.Cement.Id, y.Cement.UnitId, 150, null, null)]));
        await requests.SubmitAsync(req.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.MaterialRequest, req.Id);
        await requests.IssueAsync(req.Id, new IssueRequest(null));

        (await projects.SummaryAsync(y.VillaId)).MaterialCost.Should().Be(40_000m);
        (await projects.SummaryAsync(neighbour.Id)).MaterialCost.Should().Be(60_000m);

        // Inventory is site-level: one pool, two villas drawing from it, and 250 bags gone.
        (await sp.GetRequiredService<IInventoryService>().BalancesAsync(y.SiteId, null, false, null))
            .Single(b => b.MaterialId == y.Cement.Id).Quantity.Should().Be(250m);
    }
}
