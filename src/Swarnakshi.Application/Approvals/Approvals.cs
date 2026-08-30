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
    IEnumerable<IApprovalHandler> handlers) : IApprovalService
{
    private IApprovalHandler Handler(string entityType) =>
        handlers.FirstOrDefault(h => h.EntityType == entityType)
        ?? throw new AppException($"No approval handler registered for '{entityType}'.", 500);

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

        var req = new ApprovalRequest
        {
            EntityType = entityType, EntityId = entityId, EntityRef = entityRef,
            SiteId = siteId, ProjectId = projectId, Amount = amount,
            CurrentStatus = TransactionStatus.PendingApproval,
            RequestedByUserId = currentUser.UserId!.Value,
            RequestedAt = clock.Now
        };
        db.ApprovalRequests.Add(req);
        db.ApprovalHistories.Add(new ApprovalHistory
        {
            Request = req, Action = ApprovalAction.Submitted,
            PreviousStatus = TransactionStatus.Draft, NewStatus = TransactionStatus.PendingApproval,
            UserId = currentUser.UserId!.Value, At = clock.Now
        });
        await db.SaveChangesAsync(ct);
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

        await using var txn = await db.Database.BeginTransactionAsync(ct);

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

        await db.SaveChangesAsync(ct);
        await txn.CommitAsync(ct);

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
