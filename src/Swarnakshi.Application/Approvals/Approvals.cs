using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Approvals;

public static class ApprovalEntityTypes
{
    public const string MaterialRequest = "MaterialRequest";
    public const string Purchase = "Purchase";
    public const string ContractorPayment = "ContractorPayment";
    public const string LabourEntry = "LabourEntry";
    public const string CustomerPayment = "CustomerPayment";
    public const string InventoryAdjustment = "InventoryAdjustment";
    public const string EmployeePayment = "EmployeePayment";
    public const string ProjectExpense = "ProjectExpense";
    public const string SiteExpense = "SiteExpense";
    public const string SupplierPayment = "SupplierPayment";
}

public record ApprovalDecision(bool Approve, string? Remarks, bool AllowOverride);

/// <summary>Side-effect hook for one approvable entity type. Runs inside the approval DB transaction.</summary>
public interface IApprovalHandler
{
    string EntityType { get; }
    /// <summary>Optional pre-submit guard (throw AppException to block).</summary>
    Task OnSubmitAsync(Guid entityId, CancellationToken ct) => Task.CompletedTask;
    Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct);
    Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct) => Task.CompletedTask;
}

public record ApprovalListItem(Guid Id, string EntityType, Guid EntityId, string? EntityRef,
    Guid? SiteId, Guid? ProjectId, decimal? Amount, TransactionStatus Status,
    Guid RequestedByUserId, DateTimeOffset RequestedAt, string? Remarks);

public record ApprovalHistoryItem(ApprovalAction Action, TransactionStatus PreviousStatus,
    TransactionStatus NewStatus, Guid UserId, DateTimeOffset At, string? Remarks);

public interface IApprovalService
{
    Task<ApprovalRequest> SubmitAsync(string entityType, Guid entityId, string? entityRef,
        Guid? siteId, Guid? projectId, decimal? amount, CancellationToken ct = default);
    Task<ApprovalListItem> DecideAsync(Guid approvalRequestId, ApprovalDecision decision, CancellationToken ct = default);
    Task<PagedResult<ApprovalListItem>> ListAsync(PageQuery page, string? entityType, bool pendingOnly, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalHistoryItem>> HistoryAsync(Guid approvalRequestId, CancellationToken ct = default);
    Task<int> PendingCountAsync(CancellationToken ct = default);
}

public class ApprovalService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ISettingsService settings,
    IEnumerable<IApprovalHandler> handlers) : IApprovalService
{
    private IApprovalHandler Handler(string entityType) =>
        handlers.FirstOrDefault(h => h.EntityType == entityType)
        ?? throw new AppException($"No approval handler registered for '{entityType}'.", 500);

    /// <summary>
    /// The one gate every purchase, expense and payment passes through, and the only place that
    /// decides whether the Owner sees it.
    ///
    /// <para>Putting the auto-approve rule here rather than in each service is what makes the rule
    /// true. A caller cannot forget it, cannot apply it slightly differently, and a document type
    /// added later inherits it by virtue of submitting at all — the alternative, a threshold check
    /// copied into nine services, is a rule that holds until someone writes the tenth.</para>
    /// </summary>
    public async Task<ApprovalRequest> SubmitAsync(string entityType, Guid entityId, string? entityRef,
        Guid? siteId, Guid? projectId, decimal? amount, CancellationToken ct = default)
    {
        var handler = Handler(entityType);

        var existing = await db.ApprovalRequests
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(ct);
        if (existing is not null && existing.CurrentStatus is TransactionStatus.PendingApproval or TransactionStatus.Submitted)
            throw new AppException("This item is already awaiting approval.", 409);
        if (existing is not null && existing.CurrentStatus is TransactionStatus.Approved or TransactionStatus.Posted)
            throw new AppException("This item is already approved.", 409);

        await handler.OnSubmitAsync(entityId, ct);

        var uid = currentUser.UserId!.Value;
        var req = new ApprovalRequest
        {
            EntityType = entityType, EntityId = entityId, EntityRef = entityRef,
            SiteId = siteId, ProjectId = projectId, Amount = amount,
            CurrentStatus = TransactionStatus.PendingApproval,
            RequestedByUserId = uid,
            RequestedAt = clock.Now
        };
        db.ApprovalRequests.Add(req);
        db.ApprovalHistories.Add(new ApprovalHistory
        {
            Request = req, Action = ApprovalAction.Submitted,
            PreviousStatus = TransactionStatus.Draft, NewStatus = TransactionStatus.PendingApproval,
            UserId = uid, At = clock.Now
        });

        // Three conditions, and each one fails safe. The limit defaults to 0, so a company that has
        // never opened the settings screen approves nothing automatically. An amount the caller
        // could not work out is null, and an unknown amount is never "small". And the comparison is
        // strictly less-than, so a limit of 5,000 holds a 5,000 payment — a round number is exactly
        // what an invoice gets split into to slip under a threshold.
        // Saved as pending FIRST, unconditionally, and only then auto-approved. If the posting
        // below fails, what is left behind is an ordinary request sitting in the Owner's queue —
        // which is recoverable. Committing the request and the posting as one unit would instead
        // roll the request away too, leaving a document marked PendingApproval that appears in
        // nobody's list and can never be approved.
        await db.SaveChangesAsync(ct);

        var limit = await settings.AutoApproveLimitAsync(siteId, ct);
        if (limit <= 0m || amount is not { } value || value >= limit) return req;

        var note = $"Auto-approved: {value.ToString("N2", CultureInfo.InvariantCulture)} is below the "
                 + $"auto-approve limit of {limit.ToString("N2", CultureInfo.InvariantCulture)}.";

        // Same transaction discipline as a human decision: everything the posting touches, and the
        // request's move to Posted, land together or not at all.
        await db.ExecuteInTransactionAsync(async () =>
        {
            await handler.OnApprovedAsync(entityId, new ApprovalDecision(true, note, false), uid, ct);
            req.CurrentStatus = TransactionStatus.Posted;
            req.Remarks = note;
            // DecidedByUserId stays null on purpose. Nobody decided this, and naming the person who
            // entered it would put their name against approvals they were never asked for.
            req.DecidedAt = clock.Now;

            db.ApprovalHistories.Add(new ApprovalHistory
            {
                Request = req, Action = ApprovalAction.AutoApproved,
                PreviousStatus = TransactionStatus.PendingApproval, NewStatus = TransactionStatus.Posted,
                UserId = uid, At = clock.Now, Remarks = note
            });
        }, ct);

        return req;
    }

    public async Task<ApprovalListItem> DecideAsync(Guid approvalRequestId, ApprovalDecision decision, CancellationToken ct = default)
    {
        if (!currentUser.Has(Permissions.ApprovalsDecide))
            throw new ForbiddenException("Only an Owner (or a permitted Sub-Owner) can approve.");

        var req = await db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId, ct)
                  ?? throw new NotFoundException("ApprovalRequest", approvalRequestId);

        if (req.CurrentStatus is not TransactionStatus.PendingApproval)
            throw new AppException($"This request is '{req.CurrentStatus}' and can no longer be decided.", 409);

        var handler = Handler(req.EntityType);
        var uid = currentUser.UserId!.Value;
        var prev = req.CurrentStatus;

        // Approving is where a document turns into stock movements, ledger rows and project cost
        // all at once. If any part of that fails the request must stay pending, not sit there
        // marked Posted with half its consequences written.
        await db.ExecuteInTransactionAsync(async () =>
        {
            if (decision.Approve)
            {
                await handler.OnApprovedAsync(req.EntityId, decision, uid, ct);
                req.CurrentStatus = TransactionStatus.Posted;
            }
            else
            {
                await handler.OnRejectedAsync(req.EntityId, decision, uid, ct);
                req.CurrentStatus = TransactionStatus.Rejected;
            }

            req.DecidedByUserId = uid;
            req.DecidedAt = clock.Now;
            req.Remarks = decision.Remarks;

            db.ApprovalHistories.Add(new ApprovalHistory
            {
                ApprovalRequestId = req.Id,
                Action = decision.Approve ? ApprovalAction.Approved : ApprovalAction.Rejected,
                PreviousStatus = prev, NewStatus = req.CurrentStatus,
                UserId = uid, At = clock.Now, Remarks = decision.Remarks
            });
        }, ct);

        return ToItem(req);
    }

    public async Task<PagedResult<ApprovalListItem>> ListAsync(PageQuery page, string? entityType, bool pendingOnly, CancellationToken ct = default)
    {
        var q = db.ApprovalRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (pendingOnly) q = q.Where(a => a.CurrentStatus == TransactionStatus.PendingApproval);
        return await q.OrderByDescending(a => a.RequestedAt)
            .Select(a => new ApprovalListItem(a.Id, a.EntityType, a.EntityId, a.EntityRef,
                a.SiteId, a.ProjectId, a.Amount, a.CurrentStatus, a.RequestedByUserId, a.RequestedAt, a.Remarks))
            .ToPagedAsync(page, ct);
    }

    public async Task<IReadOnlyList<ApprovalHistoryItem>> HistoryAsync(Guid approvalRequestId, CancellationToken ct = default)
        => await db.ApprovalHistories.AsNoTracking()
            .Where(h => h.ApprovalRequestId == approvalRequestId)
            .OrderBy(h => h.At)
            .Select(h => new ApprovalHistoryItem(h.Action, h.PreviousStatus, h.NewStatus, h.UserId, h.At, h.Remarks))
            .ToListAsync(ct);

    public Task<int> PendingCountAsync(CancellationToken ct = default)
        => db.ApprovalRequests.CountAsync(a => a.CurrentStatus == TransactionStatus.PendingApproval, ct);

    private static ApprovalListItem ToItem(ApprovalRequest a) =>
        new(a.Id, a.EntityType, a.EntityId, a.EntityRef, a.SiteId, a.ProjectId, a.Amount,
            a.CurrentStatus, a.RequestedByUserId, a.RequestedAt, a.Remarks);
}
