using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;

namespace Swarnakshi.Tests;

/// <summary>
/// A purchase no longer posts the moment it is submitted — it waits for the owner. Tests that only
/// need stock on the shelf say so in one line here rather than repeating the queue dance, and the
/// tests that are actually *about* approval still drive <see cref="IApprovalService"/> directly.
/// </summary>
public static class ApprovalHelpers
{
    /// <summary>Submits a purchase and approves it, returning the posted purchase.</summary>
    public static async Task<PurchaseDto> SubmitAndApproveAsync(
        this IServiceProvider sp, Guid purchaseId, CancellationToken ct = default)
    {
        var purchases = sp.GetRequiredService<IPurchaseService>();
        var submitted = await purchases.SubmitAsync(purchaseId, ct);

        // A site with approvals switched off posts on submit; nothing is left to approve.
        if (submitted.Status == TransactionStatus.Posted) return submitted;

        await sp.ApproveAsync(ApprovalEntityTypes.Purchase, purchaseId, ct);
        return await purchases.GetAsync(purchaseId, ct);
    }

    /// <summary>Approves the one request outstanding against a specific entity.</summary>
    public static async Task ApproveAsync(
        this IServiceProvider sp, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var request = await db.ApprovalRequests.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId
                        && (a.CurrentStatus == TransactionStatus.PendingApproval
                            || a.CurrentStatus == TransactionStatus.Submitted))
            .OrderByDescending(a => a.RequestedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                $"Nothing is awaiting approval for {entityType} {entityId}.");

        await sp.GetRequiredService<IApprovalService>()
            .DecideAsync(request.Id, new ApprovalDecision(true, "Approved by the test.", false), ct);
    }
}
