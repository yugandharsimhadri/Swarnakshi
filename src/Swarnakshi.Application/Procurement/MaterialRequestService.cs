using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Procurement;

public record MaterialRequestItemInput(Guid MaterialId, Guid UnitId, decimal RequestedQty,
    Guid? ExpenseHeadId, Guid? ExpenseSubheadId);

public record SaveMaterialRequestRequest(Guid ProjectId, MaterialRequestType RequestType, DateOnly Date,
    string? Notes, List<MaterialRequestItemInput> Items);

public record IssueItemInput(Guid ItemId, decimal Quantity);

/// <summary>
/// Items to issue, and when it happened.
///
/// <para><see cref="Date"/> matters more than it looks. Site activity is typed up in the evening or
/// on a Saturday, and without a date the cost lands on the day it was typed rather than the day the
/// material left the store. Material issued on 31 March and entered on 2 April then falls in April,
/// which quietly breaks month-end. Left null it falls back to the request's own date.</para>
/// </summary>
public record IssueRequest(List<IssueItemInput>? Items, DateOnly? Date = null);

public record MaterialRequestItemDto(Guid Id, Guid MaterialId, string MaterialName, string UnitCode,
    decimal RequestedQty, decimal? ApprovedQty, decimal IssuedQty, Guid? ExpenseHeadId, Guid? ExpenseSubheadId);

public record MaterialRequestDto(Guid Id, string TxnNumber, Guid SiteId, string SiteName, Guid ProjectId,
    string ProjectName, MaterialRequestType RequestType, MaterialRequestStatus RequestStatus,
    TransactionStatus Status, DateOnly Date, string? Notes, IReadOnlyList<MaterialRequestItemDto> Items);

public class SaveMaterialRequestValidator : AbstractValidator<SaveMaterialRequestRequest>
{
    public SaveMaterialRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.RequestedQty).GreaterThan(0));
    }
}

// ---- Issuer (shared) --------------------------------------------------
public class MaterialRequestIssuer(
    IAppDbContext db, IInventoryLedger ledger, IProjectCostWriter costWriter)
{
    /// <summary>
    /// Moves approved stock to the project, records consumption + project material cost.
    /// <paramref name="on"/> is the day the material actually left the store; null falls back to the
    /// request's own date, never to "now".
    /// </summary>
    public async Task IssueAsync(Guid requestId, Guid actorId, IReadOnlyDictionary<Guid, decimal>? overrides,
        DateOnly? on, CancellationToken ct)
    {
        var req = await db.MaterialRequests.Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("MaterialRequest", requestId);

        if (req.RequestStatus is not (MaterialRequestStatus.Approved or MaterialRequestStatus.PartiallyIssued))
            throw new AppException($"Request {req.TxnNumber} is {req.RequestStatus}; it must be approved before issue.", 409);

        // The ledger entry and the cost row must carry the same date, and it must be the date the
        // material moved — not the date somebody got round to typing it in.
        var issuedOn = on ?? req.Date;

        var anyIssued = false;
        var anyPending = false;

        foreach (var item in req.Items)
        {
            var approved = item.ApprovedQty ?? item.RequestedQty;
            var remaining = approved - item.IssuedQty;
            if (remaining <= 0) continue;

            var toIssue = overrides is not null && overrides.TryGetValue(item.Id, out var v)
                ? Math.Min(v, remaining)
                : remaining;
            if (toIssue <= 0) { anyPending = true; continue; }

            var (txn, rate) = await ledger.IssueAsync(req.SiteId, item.MaterialId, item.UnitId, toIssue,
                InventoryTransactionType.ProjectConsumption, issuedOn, ApprovalEntityTypes.MaterialRequest,
                req.Id, req.TxnNumber, req.ProjectId, null, actorId, ct);

            item.IssuedQty += toIssue;
            item.Rate = rate;
            anyIssued = true;
            if (item.IssuedQty < approved) anyPending = true;

            await costWriter.WriteMaterialCostAsync(req.ProjectId, Math.Round(toIssue * rate, 2), issuedOn,
                item.ExpenseHeadId, item.ExpenseSubheadId, "InventoryTransaction", txn.Id,
                $"Consumption: {req.TxnNumber}", ct);
        }

        if (!anyIssued) throw new AppException("Nothing left to issue on this request.", 409);

        req.RequestStatus = anyPending ? MaterialRequestStatus.PartiallyIssued : MaterialRequestStatus.Issued;
        req.Status = TransactionStatus.Posted;
        await db.SaveChangesAsync(ct);
    }
}

public interface IMaterialRequestService
{
    Task<PagedResult<MaterialRequestDto>> ListAsync(PageQuery page, Guid? projectId, Guid? siteId, MaterialRequestStatus? status, CancellationToken ct = default);
    Task<MaterialRequestDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<MaterialRequestDto> CreateAsync(SaveMaterialRequestRequest req, CancellationToken ct = default);
    Task<MaterialRequestDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<MaterialRequestDto> IssueAsync(Guid id, IssueRequest req, CancellationToken ct = default);
    Task<MaterialRequestDto> CancelAsync(Guid id, CancellationToken ct = default);
}

public class MaterialRequestService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IApprovalService approvals,
    MaterialRequestIssuer issuer,
    ITransactionSequenceService sequences,
    IValidator<SaveMaterialRequestRequest> validator) : IMaterialRequestService
{
    public async Task<PagedResult<MaterialRequestDto>> ListAsync(PageQuery page, Guid? projectId, Guid? siteId, MaterialRequestStatus? status, CancellationToken ct = default)
    {
        var q = db.MaterialRequests.AsNoTracking();
        if (projectId is not null) q = q.Where(r => r.ProjectId == projectId);
        if (siteId is not null) q = q.Where(r => r.SiteId == siteId);
        if (status is not null) q = q.Where(r => r.RequestStatus == status);
        if (!string.IsNullOrWhiteSpace(page.Q)) q = q.Where(r => r.TxnNumber.Contains(page.Q));
        return await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<MaterialRequestDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.MaterialRequests.AsNoTracking().Where(r => r.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("MaterialRequest", id);

    public async Task<MaterialRequestDto> CreateAsync(SaveMaterialRequestRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct)
                      ?? throw new NotFoundException("Project", req.ProjectId);

        var entity = new MaterialRequest
        {
            TxnNumber = await sequences.NextAsync("MATREQ", ct),
            SiteId = project.SiteId, ProjectId = project.Id, RequestType = req.RequestType,
            RequestStatus = MaterialRequestStatus.Draft, Status = TransactionStatus.Draft,
            RequestedByUserId = currentUser.UserId!.Value, Date = req.Date, Notes = req.Notes
        };
        foreach (var i in req.Items)
            entity.Items.Add(new MaterialRequestItem
            {
                MaterialId = i.MaterialId, UnitId = i.UnitId, RequestedQty = i.RequestedQty,
                ExpenseHeadId = i.ExpenseHeadId, ExpenseSubheadId = i.ExpenseSubheadId
            });

        db.MaterialRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<MaterialRequestDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.MaterialRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new NotFoundException("MaterialRequest", id);
        if (entity.RequestStatus != MaterialRequestStatus.Draft)
            throw new AppException($"Request is already {entity.RequestStatus}.", 409);

        entity.RequestStatus = MaterialRequestStatus.PendingApproval;
        entity.Status = TransactionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);

        await approvals.SubmitAsync(ApprovalEntityTypes.MaterialRequest, entity.Id, entity.TxnNumber,
            entity.SiteId, entity.ProjectId, null, ct);
        return await GetAsync(id, ct);
    }

    public async Task<MaterialRequestDto> IssueAsync(Guid id, IssueRequest req, CancellationToken ct = default)
    {
        var overrides = req.Items?.ToDictionary(x => x.ItemId, x => x.Quantity);
        await using var txn = await db.Database.BeginTransactionAsync(ct);
        await issuer.IssueAsync(id, currentUser.UserId!.Value, overrides, req.Date, ct);
        await txn.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<MaterialRequestDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.MaterialRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                     ?? throw new NotFoundException("MaterialRequest", id);
        if (entity.RequestStatus is MaterialRequestStatus.Issued or MaterialRequestStatus.Cancelled)
            throw new AppException($"Cannot cancel a {entity.RequestStatus} request.", 409);
        entity.RequestStatus = MaterialRequestStatus.Cancelled;
        entity.Status = TransactionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly Expression<Func<MaterialRequest, MaterialRequestDto>> Projection = r => new MaterialRequestDto(
        r.Id, r.TxnNumber, r.SiteId, r.Site.Name, r.ProjectId, r.Project.Name, r.RequestType, r.RequestStatus,
        r.Status, r.Date, r.Notes,
        r.Items.Select(i => new MaterialRequestItemDto(i.Id, i.MaterialId, i.Material.Name, i.Unit.Code,
            i.RequestedQty, i.ApprovedQty, i.IssuedQty, i.ExpenseHeadId, i.ExpenseSubheadId)).ToList());
}

public class MaterialRequestApprovalHandler(IAppDbContext db) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.MaterialRequest;

    public async Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var req = await db.MaterialRequests.Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == entityId, ct)
            ?? throw new NotFoundException("MaterialRequest", entityId);

        req.RequestStatus = MaterialRequestStatus.Approved;
        req.Status = TransactionStatus.Approved;
        req.ApprovedBy = decidedBy;
        req.ApprovedAt = DateTimeOffset.UtcNow;
        foreach (var item in req.Items)
            item.ApprovedQty ??= item.RequestedQty;
        await db.SaveChangesAsync(ct);
    }

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var req = await db.MaterialRequests.FirstOrDefaultAsync(r => r.Id == entityId, ct);
        if (req is null) return;
        req.RequestStatus = MaterialRequestStatus.Rejected;
        req.Status = TransactionStatus.Rejected;
        await db.SaveChangesAsync(ct);
    }
}
