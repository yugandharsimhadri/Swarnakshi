using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Expenses;

public record LabourEntryDto(Guid Id, string TxnNumber, Guid ProjectId, string ProjectName, Guid LabourCategoryId,
    string LabourCategoryName, LabourPeriodType PeriodType, DateOnly PeriodStart, DateOnly PeriodEnd,
    decimal Amount, string? PaymentType, string? Remarks, TransactionStatus Status);

public record SaveLabourEntryRequest(Guid ProjectId, Guid LabourCategoryId, LabourPeriodType PeriodType,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount, Guid? PaymentMethodId, string? PaymentType, string? Remarks);

public class SaveLabourEntryValidator : AbstractValidator<SaveLabourEntryRequest>
{
    public SaveLabourEntryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.LabourCategoryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart);
    }
}

public interface ILabourService
{
    Task<PagedResult<LabourEntryDto>> ListAsync(PageQuery page, Guid? projectId, TransactionStatus? status, CancellationToken ct = default);
    Task<LabourEntryDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<LabourEntryDto> CreateAsync(SaveLabourEntryRequest req, CancellationToken ct = default);
    Task<LabourEntryDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<LabourEntryDto> CancelAsync(Guid id, CancellationToken ct = default);
}

public class LabourService(
    IAppDbContext db, ICurrentUser currentUser, IApprovalService approvals,
    ITransactionSequenceService sequences, IValidator<SaveLabourEntryRequest> validator) : ILabourService
{
    public async Task<PagedResult<LabourEntryDto>> ListAsync(PageQuery page, Guid? projectId, TransactionStatus? status, CancellationToken ct = default)
    {
        var q = db.LabourEntries.AsNoTracking();
        if (projectId is not null) q = q.Where(l => l.ProjectId == projectId);
        if (status is not null) q = q.Where(l => l.Status == status);
        return await q.OrderByDescending(l => l.PeriodEnd).ThenByDescending(l => l.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<LabourEntryDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.LabourEntries.AsNoTracking().Where(l => l.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("LabourEntry", id);

    public async Task<LabourEntryDto> CreateAsync(SaveLabourEntryRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct)) throw new NotFoundException("Project", req.ProjectId);
        if (!await db.LabourCategories.AnyAsync(c => c.Id == req.LabourCategoryId && c.IsActive, ct))
            throw new AppException("Labour category not found or inactive.", 400);

        var entry = new LabourEntry
        {
            TxnNumber = await sequences.NextAsync("LAB", ct),
            ProjectId = req.ProjectId, LabourCategoryId = req.LabourCategoryId, PeriodType = req.PeriodType,
            PeriodStart = req.PeriodStart, PeriodEnd = req.PeriodEnd, Amount = req.Amount,
            PaymentMethodId = req.PaymentMethodId, PaymentType = req.PaymentType, Remarks = req.Remarks,
            Status = TransactionStatus.Draft
        };
        db.LabourEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entry.Id, ct);
    }

    public async Task<LabourEntryDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await db.LabourEntries.FirstOrDefaultAsync(l => l.Id == id, ct)
                    ?? throw new NotFoundException("LabourEntry", id);
        if (entry.Status != TransactionStatus.Draft) throw new AppException($"Labour entry is already {entry.Status}.", 409);
        entry.Status = TransactionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        await approvals.SubmitAsync(ApprovalEntityTypes.LabourEntry, entry.Id, entry.TxnNumber,
            null, entry.ProjectId, entry.Amount, ct);
        return await GetAsync(id, ct);
    }

    public async Task<LabourEntryDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await db.LabourEntries.FirstOrDefaultAsync(l => l.Id == id, ct)
                    ?? throw new NotFoundException("LabourEntry", id);
        if (entry.Status is TransactionStatus.Posted or TransactionStatus.Cancelled)
            throw new AppException($"Cannot cancel a {entry.Status} labour entry.", 409);
        entry.Status = TransactionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly Expression<Func<LabourEntry, LabourEntryDto>> Projection = l => new LabourEntryDto(
        l.Id, l.TxnNumber, l.ProjectId, l.Project.Name, l.LabourCategoryId, l.LabourCategory.Name,
        l.PeriodType, l.PeriodStart, l.PeriodEnd, l.Amount, l.PaymentType, l.Remarks, l.Status);
}

public class LabourApprovalHandler(IAppDbContext db, IProjectCostWriter costWriter) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.LabourEntry;

    public async Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var entry = await db.LabourEntries.FirstOrDefaultAsync(l => l.Id == entityId, ct)
                    ?? throw new NotFoundException("LabourEntry", entityId);
        if (entry.Status == TransactionStatus.Posted) return;

        entry.Status = TransactionStatus.Posted;
        entry.ApprovedBy = decidedBy;
        entry.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var labourHead = await db.ExpenseHeads.Where(h => h.Name == "Labour").Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct);
        await costWriter.WriteAsync(entry.ProjectId, ProjectExpenseType.Labour, entry.Amount, entry.PeriodEnd,
            labourHead, null, $"Labour: {entry.TxnNumber}", "LabourEntry", entry.Id, ct);
    }

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var entry = await db.LabourEntries.FirstOrDefaultAsync(l => l.Id == entityId, ct);
        if (entry is null) return;
        entry.Status = TransactionStatus.Rejected;
        await db.SaveChangesAsync(ct);
    }
}
