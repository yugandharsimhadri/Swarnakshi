using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Paying a supplier used to be the one way money left the company without anyone agreeing to it:
/// the payment was written and the invoice's outstanding balance moved in the same call. It now
/// waits, and these are the four things that has to keep true.
/// </summary>
public class SupplierPaymentApprovalTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid SiteId, Guid SupplierId, Material Cement);

    private static async Task<Yard> ArrangeAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var supplier = new Supplier { Code = "SUP1", Name = "Sri Balaji Traders" };
        db.AddRange(site, supplier);
        await db.SaveChangesAsync();
        var cement = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");
        return new Yard(site.Id, supplier.Id, cement);
    }

    /// <summary>A posted purchase of 100 × 400 = ₹40,000, ready to be paid against.</summary>
    private static async Task<PurchaseDto> InvoiceAsync(IServiceProvider sp, Yard y)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var created = await purchases.CreateAsync(new SavePurchaseRequest(
            y.SupplierId, null, y.SiteId, null, "INV-1", null, Today, 0, null,
            [new PurchaseItemInput(y.Cement.Id, y.Cement.UnitId, 100, 400, 0, 0)]));
        return await sp.SubmitAndApproveAsync(created.Id);
    }

    [Fact]
    public async Task A_supplier_payment_does_not_move_the_balance_until_it_is_approved()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var y = await ArrangeAsync(db);
        var invoice = await InvoiceAsync(sp, y);

        var afterRaising = await purchases.AddPaymentAsync(invoice.Id,
            new SupplierPaymentInput(15_000, Today, null, "NEFT-1"));

        afterRaising.PaidAmount.Should().Be(0, "nothing has been agreed yet");
        afterRaising.BalanceAmount.Should().Be(40_000);
        afterRaising.PaymentStatus.Should().Be(PaymentStatus.Unpaid);

        var payment = await db.SupplierPayments.AsNoTracking().SingleAsync();
        payment.Status.Should().Be(TransactionStatus.PendingApproval);

        await sp.ApproveAsync(ApprovalEntityTypes.SupplierPayment, payment.Id);

        var afterApproval = await purchases.GetAsync(invoice.Id);
        afterApproval.PaidAmount.Should().Be(15_000);
        afterApproval.BalanceAmount.Should().Be(25_000);
        afterApproval.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task A_rejected_supplier_payment_leaves_the_invoice_untouched()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var y = await ArrangeAsync(db);
        var invoice = await InvoiceAsync(sp, y);

        await purchases.AddPaymentAsync(invoice.Id, new SupplierPaymentInput(40_000, Today, null, null));

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 },
            ApprovalEntityTypes.SupplierPayment, true);
        await approvals.DecideAsync(pending.Items[0].Id,
            new ApprovalDecision(false, "supplier has not delivered the last lorry", false));

        var invoiceAfter = await purchases.GetAsync(invoice.Id);
        invoiceAfter.PaidAmount.Should().Be(0);
        invoiceAfter.BalanceAmount.Should().Be(40_000);
        (await db.SupplierPayments.AsNoTracking().SingleAsync()).Status
            .Should().Be(TransactionStatus.Rejected);
    }

    [Fact]
    public async Task Payments_waiting_for_approval_still_count_against_the_balance()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var y = await ArrangeAsync(db);
        var invoice = await InvoiceAsync(sp, y);

        await purchases.AddPaymentAsync(invoice.Id, new SupplierPaymentInput(30_000, Today, null, null));

        // The invoice still reads as ₹40,000 outstanding, because the first payment has not been
        // approved. Without counting what is already in the queue, this second one would sail past
        // the check and the supplier would end up approved for ₹55,000 against a ₹40,000 invoice.
        var act = () => purchases.AddPaymentAsync(invoice.Id, new SupplierPaymentInput(25_000, Today, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*awaiting approval*");
    }

    [Fact]
    public async Task A_payment_below_the_limit_pays_the_invoice_without_asking()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var y = await ArrangeAsync(db);
        var invoice = await InvoiceAsync(sp, y);

        await sp.SetAutoApproveLimitAsync(5_000m);

        var after = await purchases.AddPaymentAsync(invoice.Id,
            new SupplierPaymentInput(2_000, Today, null, "cash"));

        after.PaidAmount.Should().Be(2_000);
        after.BalanceAmount.Should().Be(38_000);
        (await approvals.PendingCountAsync()).Should().Be(0);
        (await db.SupplierPayments.AsNoTracking().SingleAsync()).Status
            .Should().Be(TransactionStatus.Posted);
    }
}
