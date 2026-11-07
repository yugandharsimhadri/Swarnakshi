using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Reports;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Correcting or removing a contractor payment that has already been posted.
///
/// <para>Three figures hang off a posted payment: the payment, what the work order says has been
/// paid, and the villa's cost. They have to agree before the change, stay exactly as they were while
/// it waits, and agree again after it — with the villa charged once. Every test here is about that,
/// or about who may do it.</para>
/// </summary>
public class ContractorPaymentChangeTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid ProjectId, Guid OtherProjectId, Guid ContractorId, Guid WorkId, Guid MethodId,
        Guid EngineerId, Guid OwnerId);

    private static async Task<Yard> ArrangeAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var site = new Site { Code = "S1", Name = "Green Meadows", Status = SiteStatus.Active };
        var villa = new Project { Code = "P1", Name = "Villa 12", Site = site, EstimatedCost = 2_000_000, Status = ProjectStatus.Active };
        var other = new Project { Code = "P2", Name = "Villa 7", Site = site, EstimatedCost = 2_000_000, Status = ProjectStatus.Active };
        var contractor = new Contractor { Code = "C1", Name = "Raju Plumbing" };
        var engineer = new User
        {
            Name = "Ramesh", Username = "ramesh", Role = UserRole.Engineer,
            PasswordHash = sp.GetRequiredService<IPasswordHasher>().Hash("Secure@123")
        };
        db.AddRange(site, villa, other, contractor, engineer);
        await db.SaveChangesAsync();

        var work = await sp.GetRequiredService<IContractWorkService>().CreateAsync(new SaveContractWorkRequest(
            villa.Id, contractor.Id, "Plumbing", null, 100_000m, null, null, null, ContractWorkStatus.Active));
        var method = await db.PaymentMethods.AsNoTracking().FirstAsync();
        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Role == UserRole.Owner);
        return new Yard(villa.Id, other.Id, contractor.Id, work.Id, method.Id, engineer.Id, owner.Id);
    }

    private static SaveContractorPaymentRequest Payment(Yard y, decimal amount, Guid? workId = null, Guid? projectId = null, DateOnly? date = null) =>
        new(y.ContractorId, projectId ?? y.ProjectId, workId, date ?? Today, amount, y.MethodId, null, null, ContractorPaymentKind.Partial);

    private static async Task<ContractorPaymentDto> PostedAsync(IServiceProvider sp, Yard y, decimal amount)
    {
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var created = await payments.CreateAsync(Payment(y, amount, y.WorkId));
        await payments.SubmitAsync(created.Id);
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.ContractorPayment, created.Id);
        return await payments.GetAsync(created.Id);
    }

    private static async Task<Guid> ChangeIdAsync(IServiceProvider sp, Guid paymentId) =>
        await sp.GetRequiredService<AppDbContext>().ContractorPaymentChanges.AsNoTracking()
            .Where(c => c.ContractorPaymentId == paymentId)
            .OrderByDescending(c => c.CreatedAt).Select(c => c.Id).FirstAsync();

    private static Task ApproveChangeAsync(IServiceProvider sp, Guid changeId) =>
        sp.ApproveAsync(ApprovalEntityTypes.ContractorPaymentChange, changeId);

    /// <summary>The three figures, read fresh so nothing tracked in the scope flatters the answer.</summary>
    private static async Task<(decimal Payment, decimal WorkPaid, decimal WorkBalance, decimal VillaCost, int CostRows)> FiguresAsync(
        TestHost host, Yard y, Guid paymentId)
    {
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await db.ContractorPayments.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        var work = await db.ContractWorks.AsNoTracking().FirstAsync(w => w.Id == y.WorkId);
        var costs = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.SourceType == "ContractorPayment" && e.SourceId == paymentId && e.Status == TransactionStatus.Posted)
            .ToListAsync();
        return (payment.Amount, work.TotalPaid, work.Balance, costs.Sum(c => c.Amount), costs.Count);
    }

    [Fact]
    public async Task An_edit_changes_nothing_until_approved_and_then_all_three_figures_move_together()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));
        var asked = await payments.RequestChangeAsync(paid.Id, Payment(y, 25_000m, y.WorkId, date: Today.AddDays(-2)), "Typed 30k, paid 25k");

        asked.PendingChange.Should().Be(ContractorPaymentChangeKind.Edit);
        (await FiguresAsync(host, y, paid.Id)).Should().Be((30_000m, 30_000m, 70_000m, 30_000m, 1));

        host.ActAs(y.OwnerId);
        await ApproveChangeAsync(sp, await ChangeIdAsync(sp, paid.Id));

        // One cost row, rewritten. A second row beside the first is the double count this guards.
        (await FiguresAsync(host, y, paid.Id)).Should().Be((25_000m, 25_000m, 75_000m, 25_000m, 1));

        var after = await payments.GetAsync(paid.Id);
        after.Date.Should().Be(Today.AddDays(-2));
        after.Status.Should().Be(TransactionStatus.Posted);
        after.PendingChange.Should().BeNull();
    }

    [Fact]
    public async Task A_delete_voids_the_payment_and_gives_back_the_contract_and_the_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));
        await payments.RequestDeleteAsync(paid.Id, "Entered against the wrong villa");
        (await FiguresAsync(host, y, paid.Id)).VillaCost.Should().Be(30_000m);

        host.ActAs(y.OwnerId);
        var changeId = await ChangeIdAsync(sp, paid.Id);
        await ApproveChangeAsync(sp, changeId);

        var f = await FiguresAsync(host, y, paid.Id);
        f.WorkPaid.Should().Be(0m);
        f.WorkBalance.Should().Be(100_000m);
        f.VillaCost.Should().Be(0m);
        f.CostRows.Should().Be(0);

        // Voided, never removed, and the change still says what the figure was.
        (await payments.GetAsync(paid.Id)).Status.Should().Be(TransactionStatus.Cancelled);
        var change = await payments.GetChangeAsync(changeId);
        change.Before.Amount.Should().Be(30_000m);
        change.Reason.Should().Be("Entered against the wrong villa");
    }

    [Fact]
    public async Task A_rejected_change_leaves_the_payment_alone_and_another_can_be_raised()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var approvals = sp.GetRequiredService<IApprovalService>();

        await payments.RequestDeleteAsync(paid.Id, "Duplicate");
        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 }, ApprovalEntityTypes.ContractorPaymentChange, true);
        await approvals.DecideAsync(pending.Items.Single().Id, new ApprovalDecision(false, "It is not a duplicate", false));

        (await FiguresAsync(host, y, paid.Id)).Should().Be((30_000m, 30_000m, 70_000m, 30_000m, 1));
        var after = await payments.GetAsync(paid.Id);
        after.Status.Should().Be(TransactionStatus.Posted);
        after.PendingChange.Should().BeNull();

        var again = await payments.RequestChangeAsync(paid.Id, Payment(y, 29_000m, y.WorkId), "The real correction");
        again.PendingChange.Should().Be(ContractorPaymentChangeKind.Edit);
    }

    [Fact]
    public async Task A_change_always_waits_for_a_person_whatever_the_auto_approve_limit_says()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        await sp.SetAutoApproveLimitAsync(1_000_000m);

        await sp.GetRequiredService<IContractorPaymentService>()
            .RequestChangeAsync(paid.Id, Payment(y, 500m, y.WorkId), "Small correction");

        (await FiguresAsync(host, y, paid.Id)).Payment.Should().Be(30_000m);
    }

    [Fact]
    public async Task Only_one_change_can_be_waiting_on_a_payment()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        await payments.RequestChangeAsync(paid.Id, Payment(y, 25_000m, y.WorkId), "First");

        var second = () => payments.RequestDeleteAsync(paid.Id, "Second");
        await second.Should().ThrowAsync<AppException>().WithMessage("*already waiting*");
    }

    [Fact]
    public async Task The_engineer_can_ask_but_only_an_owner_or_co_owner_can_decide()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));
        await payments.RequestChangeAsync(paid.Id, Payment(y, 25_000m, y.WorkId), "Typed it wrong");

        var byEngineer = () => ApproveChangeAsync(sp, ChangeIdAsync(sp, paid.Id).GetAwaiter().GetResult());
        await byEngineer.Should().ThrowAsync<ForbiddenException>();

        foreach (var role in new[] { UserRole.Accountant, UserRole.Supervisor })
        {
            host.ActAs(Guid.NewGuid(), role, Permissions.ForRole(role));
            var byOther = () => ApproveChangeAsync(sp, ChangeIdAsync(sp, paid.Id).GetAwaiter().GetResult());
            await byOther.Should().ThrowAsync<ForbiddenException>();
        }

        host.ActAs(Guid.NewGuid(), UserRole.SubOwner, Permissions.ForRole(UserRole.SubOwner));
        await ApproveChangeAsync(sp, await ChangeIdAsync(sp, paid.Id));
        (await FiguresAsync(host, y, paid.Id)).Payment.Should().Be(25_000m);
    }

    [Fact]
    public async Task A_payment_nobody_has_posted_is_edited_directly_and_a_posted_one_cannot_be()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        var draft = await payments.CreateAsync(Payment(y, 10_000m, y.WorkId));
        (await payments.UpdateAsync(draft.Id, Payment(y, 12_000m, y.WorkId))).Amount.Should().Be(12_000m);
        (await payments.CancelAsync(draft.Id, "Not paid after all")).Status.Should().Be(TransactionStatus.Cancelled);

        var paid = await PostedAsync(sp, y, 30_000m);
        var edit = () => payments.UpdateAsync(paid.Id, Payment(y, 1m, y.WorkId));
        await edit.Should().ThrowAsync<AppException>().WithMessage("*Ask for a change*");
        var cancel = () => payments.CancelAsync(paid.Id, "x");
        await cancel.Should().ThrowAsync<AppException>().WithMessage("*Ask for a change*");
    }

    [Fact]
    public async Task A_payment_the_owner_sent_back_can_be_corrected_and_sent_again()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var approvals = sp.GetRequiredService<IApprovalService>();

        var created = await payments.CreateAsync(Payment(y, 10_000m, y.WorkId));
        await payments.SubmitAsync(created.Id);
        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 }, ApprovalEntityTypes.ContractorPayment, true);
        await approvals.DecideAsync(pending.Items.Single().Id, new ApprovalDecision(false, "Wrong amount", false));

        (await payments.UpdateAsync(created.Id, Payment(y, 9_000m, y.WorkId))).Status.Should().Be(TransactionStatus.Draft);
        await payments.SubmitAsync(created.Id);
        await sp.ApproveAsync(ApprovalEntityTypes.ContractorPayment, created.Id);

        (await FiguresAsync(host, y, created.Id)).Should().Be((9_000m, 9_000m, 91_000m, 9_000m, 1));
    }

    [Fact]
    public async Task Waiting_payments_and_voided_ones_cannot_have_a_change_raised()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        var waiting = await payments.CreateAsync(Payment(y, 10_000m, y.WorkId));
        await payments.SubmitAsync(waiting.Id);
        var onWaiting = () => payments.RequestDeleteAsync(waiting.Id, "x");
        await onWaiting.Should().ThrowAsync<AppException>().WithMessage("*waiting for approval*");

        var paid = await PostedAsync(sp, y, 5_000m);
        await payments.RequestDeleteAsync(paid.Id, "Gone");
        await ApproveChangeAsync(sp, await ChangeIdAsync(sp, paid.Id));
        var onVoided = () => payments.RequestChangeAsync(paid.Id, Payment(y, 1m), "x");
        await onVoided.Should().ThrowAsync<AppException>().WithMessage("*Cancelled*");
    }

    [Fact]
    public async Task A_change_needs_a_reason_a_real_difference_and_the_same_contractor_and_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();

        var noReason = () => payments.RequestChangeAsync(paid.Id, Payment(y, 25_000m, y.WorkId), "  ");
        await noReason.Should().ThrowAsync<AppException>().WithMessage("*Say why*");

        var same = () => payments.RequestChangeAsync(paid.Id, Payment(y, 30_000m, y.WorkId), "No change");
        await same.Should().ThrowAsync<AppException>().WithMessage("*Nothing is different*");

        var moved = () => payments.RequestChangeAsync(paid.Id, Payment(y, 25_000m, projectId: y.OtherProjectId), "Wrong villa");
        await moved.Should().ThrowAsync<AppException>().WithMessage("*cannot be moved*");
    }

    [Fact]
    public async Task Moving_a_payment_to_another_work_order_releases_the_first_and_charges_the_second()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var paid = await PostedAsync(sp, y, 30_000m);
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var second = await sp.GetRequiredService<IContractWorkService>().CreateAsync(new SaveContractWorkRequest(
            y.ProjectId, y.ContractorId, "Sanitary", null, 50_000m, null, null, null, ContractWorkStatus.Active));

        await payments.RequestChangeAsync(paid.Id, Payment(y, 30_000m, second.Id), "Wrong work order");
        await ApproveChangeAsync(sp, await ChangeIdAsync(sp, paid.Id));

        using var read = host.Scope();
        var db = read.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ContractWorks.AsNoTracking().FirstAsync(w => w.Id == y.WorkId)).TotalPaid.Should().Be(0m);
        (await db.ContractWorks.AsNoTracking().FirstAsync(w => w.Id == second.Id)).Balance.Should().Be(20_000m);
    }

    [Fact]
    public async Task A_change_past_the_contract_balance_is_refused_and_nothing_moves()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var y = await ArrangeWithPaymentAsync(host);

        using (var act = host.Scope())
        {
            var sp = act.ServiceProvider;
            await sp.GetRequiredService<IContractorPaymentService>()
                .RequestChangeAsync(y.PaymentId, Payment(y.Yard, 150_000m, y.Yard.WorkId), "Paid more than the contract");
            var approve = async () => await ApproveChangeAsync(sp, await ChangeIdAsync(sp, y.PaymentId));
            await approve.Should().ThrowAsync<AppException>().WithMessage("*exceeds contract balance*");
        }

        (await FiguresAsync(host, y.Yard, y.PaymentId)).Should().Be((30_000m, 30_000m, 70_000m, 30_000m, 1));
    }

    private static async Task<(Yard Yard, Guid PaymentId)> ArrangeWithPaymentAsync(TestHost host)
    {
        using var scope = host.Scope();
        var y = await ArrangeAsync(scope.ServiceProvider);
        var paid = await PostedAsync(scope.ServiceProvider, y, 30_000m);
        return (y, paid.Id);
    }

    [Fact]
    public async Task The_statement_adds_up_across_villas_and_each_villa_stands_on_its_own()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var reports = sp.GetRequiredService<IReportsService>();

        await PostedAsync(sp, y, 30_000m);
        var voided = await PostedAsync(sp, y, 7_000m);
        await payments.RequestDeleteAsync(voided.Id, "Duplicate");
        await ApproveChangeAsync(sp, await ChangeIdAsync(sp, voided.Id));

        // In the second villa: no work order at all, one paid and one still waiting.
        var loose = await payments.CreateAsync(Payment(y, 12_000m, projectId: y.OtherProjectId));
        await payments.SubmitAsync(loose.Id);
        await sp.ApproveAsync(ApprovalEntityTypes.ContractorPayment, loose.Id);
        var waiting = await payments.CreateAsync(Payment(y, 4_000m, projectId: y.OtherProjectId));
        await payments.SubmitAsync(waiting.Id);

        var s = await reports.ContractorStatementAsync(y.ContractorId);

        s.ContractorName.Should().Be("Raju Plumbing");
        s.Contracted.Should().Be(100_000m);
        s.Paid.Should().Be(42_000m);
        s.Balance.Should().Be(58_000m);
        s.AwaitingApproval.Should().Be(4_000m);
        s.PaymentCount.Should().Be(2);
        s.PaidOutsideWorkOrders.Should().Be(12_000m);

        s.Villas.Should().HaveCount(2);
        var v12 = s.Villas.Single(v => v.ProjectId == y.ProjectId);
        v12.Paid.Should().Be(30_000m);
        v12.Balance.Should().Be(70_000m);
        v12.Payments.Should().ContainSingle();
        var v7 = s.Villas.Single(v => v.ProjectId == y.OtherProjectId);
        v7.Contracted.Should().Be(0m);
        v7.Paid.Should().Be(12_000m);
        v7.AwaitingApproval.Should().Be(4_000m);
        v7.Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Every_contractor_report_can_open_the_statement_for_each_row()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var y = await ArrangeAsync(sp);
        var reports = sp.GetRequiredService<IReportsService>();
        await PostedAsync(sp, y, 30_000m);

        foreach (var table in new[]
                 {
                     await reports.ContractorPaymentsAsync(),
                     await reports.ContractorOutstandingAsync(),
                     await reports.ContractorCommitmentAsync(),
                 })
        {
            table.KeyColumn.Should().Be("Contractor");
            table.RowKeys.Should().HaveCount(table.Rows.Count).And.Contain(y.ContractorId);
        }

        var paid = await reports.ContractorPaymentsAsync();
        var row = paid.Rows[paid.RowKeys!.ToList().IndexOf(y.ContractorId)];
        row.Should().ContainInOrder("Raju Plumbing", 1, 1, 30_000m, 0m);
    }

    [Fact]
    public async Task Another_companys_contractor_has_no_statement_here()
    {
        await using var theirs = await TestHost.CreateAsync();
        using var theirScope = theirs.Scope();
        var y = await ArrangeAsync(theirScope.ServiceProvider);

        await using var ours = await TestHost.CreateAsync();
        using var ourScope = ours.Scope();
        var look = () => ourScope.ServiceProvider.GetRequiredService<IReportsService>().ContractorStatementAsync(y.ContractorId);
        await look.Should().ThrowAsync<NotFoundException>();
    }
}
