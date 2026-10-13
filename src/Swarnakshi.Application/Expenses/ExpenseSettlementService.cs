using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Expenses;

/// <summary>One approved claim waiting to be paid back.</summary>
public record ClaimDto(Guid Id, string TxnNumber, Guid ProjectId, string ProjectName, DateOnly Date,
    string ExpenseHeadName, string? Description, decimal Amount, ReimbursementStatus ReimbursementStatus,
    Guid PaidByUserId, string PaidByUserName, DateTimeOffset? ApprovedAt);

/// <summary>What is owed to one person, and the claims that make it up.</summary>
public record ClaimsOwedDto(Guid UserId, string UserName, decimal Due, int Claims);

public record SettlementLineDto(Guid ProjectExpenseId, string TxnNumber, string ProjectName, decimal Amount);

public record SettlementDto(Guid Id, string TxnNumber, Guid PaidToUserId, string PaidToUserName,
    DateOnly Date, decimal Amount, string? PaymentMethodName, string? Reference,
    TransactionStatus Status, IReadOnlyList<SettlementLineDto> Lines);

public record SaveSettlementRequest(Guid PaidToUserId, DateOnly Date, Guid? PaymentMethodId,
    string? Reference, IReadOnlyList<Guid> ProjectExpenseIds);

public class SaveSettlementValidator : AbstractValidator<SaveSettlementRequest>
{
    public SaveSettlementValidator()
    {
        RuleFor(x => x.PaidToUserId).NotEmpty();
        RuleFor(x => x.ProjectExpenseIds).NotEmpty().WithMessage("Pick at least one claim to settle.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public interface IExpenseSettlementService
{
    /// <summary>Approved claims. <paramref name="userId"/> narrows it to one person's.</summary>
    Task<IReadOnlyList<ClaimDto>> ClaimsAsync(ReimbursementStatus? status, Guid? userId, CancellationToken ct = default);
    Task<IReadOnlyList<ClaimsOwedDto>> OwedAsync(CancellationToken ct = default);
    Task<PagedResult<SettlementDto>> ListAsync(PageQuery page, Guid? paidToUserId, CancellationToken ct = default);
    Task<SettlementDto> CreateAsync(SaveSettlementRequest req, CancellationToken ct = default);
    Task<SettlementDto> VoidAsync(Guid id, string reason, CancellationToken ct = default);
}

/// <summary>
/// Paying people back for money they spent on the company's behalf.
///
/// <para>The one thing this service must never do is write a cost row. The claim it settles was
/// already charged to the villa when the Owner approved it; a settlement is the cash going out
/// against a debt that is already on the books. Add a ProjectExpense here — or put a ProjectId on
/// the settlement and let a report sum it alongside the expenses — and every villa an engineer
/// ever bought cement for is overstated by exactly the amount he was repaid.</para>
/// </summary>
public class ExpenseSettlementService(
    IAppDbContext db, ITransactionSequenceService sequences, IDateTimeProvider clock,
    IValidator<SaveSettlementRequest> validator) : IExpenseSettlementService
{
    public async Task<IReadOnlyList<ClaimDto>> ClaimsAsync(ReimbursementStatus? status, Guid? userId,
        CancellationToken ct = default)
    {
        var q = db.ProjectExpenses.AsNoTracking().Where(e => e.PaidByUserId != null);
        // Draft and rejected claims are nobody's debt. A claim becomes money owed when it posts.
        q = status is null
            ? q.Where(e => e.ReimbursementStatus != ReimbursementStatus.None)
            : q.Where(e => e.ReimbursementStatus == status);
        if (userId is not null) q = q.Where(e => e.PaidByUserId == userId);

        return await q.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Select(ClaimProjection).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClaimsOwedDto>> OwedAsync(CancellationToken ct = default)
    {
        var rows = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ReimbursementStatus == ReimbursementStatus.Due && e.PaidByUser != null)
            .GroupBy(e => new { e.PaidByUserId, e.PaidByUser!.Name })
            .Select(g => new ClaimsOwedDto(g.Key.PaidByUserId!.Value, g.Key.Name,
                g.Sum(x => x.Amount), g.Count()))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Due).ToList();
    }

    public async Task<PagedResult<SettlementDto>> ListAsync(PageQuery page, Guid? paidToUserId,
        CancellationToken ct = default)
    {
        var q = db.ExpenseSettlements.AsNoTracking();
        if (paidToUserId is not null) q = q.Where(s => s.PaidToUserId == paidToUserId);
        return await q.OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt)
            .Select(SettlementProjection).ToPagedAsync(page, ct);
    }

    public async Task<SettlementDto> CreateAsync(SaveSettlementRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);

        var ids = req.ProjectExpenseIds.Distinct().ToList();
        if (!await db.Users.AnyAsync(u => u.Id == req.PaidToUserId, ct))
            throw new NotFoundException("User", req.PaidToUserId);
        if (req.PaymentMethodId is { } pm && !await db.PaymentMethods.AnyAsync(m => m.Id == pm, ct))
            throw new NotFoundException("PaymentMethod", pm);

        Guid settlementId = Guid.Empty;

        // One transaction around the lot: the claims flip to Settled and the payment that settles
        // them exists together, or neither does. Half of this would be a claim marked paid with no
        // record of the payment, which is the one outcome nobody can unpick later.
        await db.ExecuteInTransactionAsync(async () =>
        {
            var claims = await db.ProjectExpenses.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
            if (claims.Count != ids.Count)
            {
                var missing = ids.Except(claims.Select(c => c.Id)).First();
                throw new NotFoundException("ProjectExpense", missing);
            }

            foreach (var claim in claims)
            {
                if (claim.PaidByUserId != req.PaidToUserId)
                    throw new AppException($"{claim.TxnNumber} was not paid by this person.", 400);
                if (claim.ReimbursementStatus != ReimbursementStatus.Due)
                    throw new AppException(claim.ReimbursementStatus == ReimbursementStatus.Settled
                        ? $"{claim.TxnNumber} has already been settled."
                        : $"{claim.TxnNumber} is not approved yet, so it cannot be settled.", 409);
            }

            var settlement = new ExpenseSettlement
            {
                TxnNumber = await sequences.NextAsync("RMB", ct),
                PaidToUserId = req.PaidToUserId,
                Date = req.Date,
                Amount = claims.Sum(c => c.Amount),
                PaymentMethodId = req.PaymentMethodId,
                Reference = req.Reference,
                // No approval gate of its own: the Owner already approved each claim, and asking
                // him to approve the payout of a debt he just agreed to adds a step and no
                // information. Who may do it is the control — see Permissions.ExpenseSettle.
                Status = TransactionStatus.Posted,
                ApprovedAt = clock.Now
            };
            foreach (var claim in claims)
            {
                settlement.Lines.Add(new ExpenseSettlementLine
                {
                    ProjectExpenseId = claim.Id,
                    Amount = claim.Amount
                });
                claim.ReimbursementStatus = ReimbursementStatus.Settled;
            }

            db.ExpenseSettlements.Add(settlement);
            await db.SaveChangesAsync(ct);
            settlementId = settlement.Id;
        }, ct);

        return await db.ExpenseSettlements.AsNoTracking()
            .Where(s => s.Id == settlementId).Select(SettlementProjection).FirstAsync(ct);
    }

    public async Task<SettlementDto> VoidAsync(Guid id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppException("Say why this settlement is being voided.", 400);

        await db.ExecuteInTransactionAsync(async () =>
        {
            var settlement = await db.ExpenseSettlements.Include(s => s.Lines)
                                 .FirstOrDefaultAsync(s => s.Id == id, ct)
                             ?? throw new NotFoundException("ExpenseSettlement", id);
            if (settlement.Status == TransactionStatus.Cancelled)
                throw new AppException("Already voided.", 409);

            // Voided, not deleted, and the lines stay with it: this is the record that the money
            // went out and came back, which is exactly what somebody will want to see later.
            settlement.Status = TransactionStatus.Cancelled;
            settlement.Remarks = reason;

            var claimIds = settlement.Lines.Select(l => l.ProjectExpenseId).ToList();
            var claims = await db.ProjectExpenses.Where(e => claimIds.Contains(e.Id)).ToListAsync(ct);
            foreach (var claim in claims)
            {
                // A cancelled claim stays cancelled — it is owed to nobody, and reviving it here
                // would resurrect a debt somebody deliberately wrote off.
                if (claim.Status == TransactionStatus.Posted)
                    claim.ReimbursementStatus = ReimbursementStatus.Due;
            }
            await db.SaveChangesAsync(ct);
        }, ct);

        return await db.ExpenseSettlements.AsNoTracking()
            .Where(s => s.Id == id).Select(SettlementProjection).FirstAsync(ct);
    }

    private static readonly Expression<Func<ProjectExpense, ClaimDto>> ClaimProjection = e => new ClaimDto(
        e.Id, e.TxnNumber, e.ProjectId, e.Project.Name, e.Date, e.Head.Name, e.Description, e.Amount,
        e.ReimbursementStatus, e.PaidByUserId!.Value, e.PaidByUser!.Name, e.ApprovedAt);

    private static readonly Expression<Func<ExpenseSettlement, SettlementDto>> SettlementProjection =
        s => new SettlementDto(s.Id, s.TxnNumber, s.PaidToUserId, s.PaidToUser.Name, s.Date, s.Amount,
            s.PaymentMethod != null ? s.PaymentMethod.Name : null, s.Reference, s.Status,
            s.Lines.Select(l => new SettlementLineDto(l.ProjectExpenseId, l.ProjectExpense.TxnNumber,
                l.ProjectExpense.Project.Name, l.Amount)).ToList());
}
