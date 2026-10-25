using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Expenses;

public record AddOnRequestDto(Guid Id, string TxnNumber, Guid ProjectId, string ProjectName, DateOnly Date,
    Guid ExpenseHeadId, string ExpenseHeadName, string Description, decimal ExpectedAmount,
    AddOnStatus Status, string? Remarks, DateTimeOffset CreatedAt);

/// <summary>
/// What the customer has asked for on one villa, by state. Four figures that are each a count and a
/// sum of estimates — never a cost, and never comparable with one.
/// </summary>
public record AddOnSummaryDto(int Requested, decimal RequestedAmount, int Agreed, decimal AgreedAmount,
    int Done, decimal DoneAmount, int Declined);

public record SaveAddOnRequest(Guid ProjectId, DateOnly Date, Guid ExpenseHeadId, string Description,
    decimal ExpectedAmount, string? Remarks);

public class SaveAddOnValidator : AbstractValidator<SaveAddOnRequest>
{
    public SaveAddOnValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ExpenseHeadId).NotEmpty().WithMessage("Pick what kind of work the change is.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000)
            .WithMessage("Say what the customer asked for.");
        // Zero is allowed on purpose: a customer often asks before anybody has worked out a price,
        // and refusing the record until someone invents a number loses the request.
        RuleFor(x => x.ExpectedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Remarks).MaximumLength(512);
    }
}

public interface IAddOnRequestService
{
    Task<IReadOnlyList<AddOnRequestDto>> ListAsync(Guid projectId, AddOnStatus? status, CancellationToken ct = default);
    Task<AddOnSummaryDto> SummaryAsync(Guid projectId, CancellationToken ct = default);
    Task<AddOnRequestDto> CreateAsync(SaveAddOnRequest req, CancellationToken ct = default);
    Task<AddOnRequestDto> UpdateAsync(Guid id, SaveAddOnRequest req, CancellationToken ct = default);
    Task<AddOnRequestDto> SetStatusAsync(Guid id, AddOnStatus status, string? remarks, CancellationToken ct = default);
}

/// <summary>
/// The register of customer changes: extra points, a different floor, a wall moved.
///
/// <para>It writes nothing but its own rows. No ProjectExpense, no touch of the villa's cost, no
/// adjustment to the sale value — the expected amount is an estimate made in conversation, and the
/// money actually spent arrives the ordinary way as expenses for the materials and labour the
/// change took. Those are what the villa is charged, once. If this service ever grows a line that
/// writes anywhere else, the same change is being counted twice.</para>
/// </summary>
public class AddOnRequestService(
    IAppDbContext db, ITransactionSequenceService sequences, IDateTimeProvider clock,
    ICurrentUser currentUser, IValidator<SaveAddOnRequest> validator) : IAddOnRequestService
{
    public async Task<IReadOnlyList<AddOnRequestDto>> ListAsync(Guid projectId, AddOnStatus? status,
        CancellationToken ct = default)
    {
        var q = db.AddOnRequests.AsNoTracking().Where(a => a.ProjectId == projectId);
        if (status is not null) q = q.Where(a => a.Status == status);
        return await q.OrderByDescending(a => a.Date).ThenByDescending(a => a.CreatedAt)
            .Select(Projection).ToListAsync(ct);
    }

    public async Task<AddOnSummaryDto> SummaryAsync(Guid projectId, CancellationToken ct = default)
    {
        var rows = await db.AddOnRequests.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(x => x.ExpectedAmount) })
            .ToListAsync(ct);

        int Count(AddOnStatus s) => rows.FirstOrDefault(r => r.Status == s)?.Count ?? 0;
        decimal Amount(AddOnStatus s) => rows.FirstOrDefault(r => r.Status == s)?.Amount ?? 0m;

        return new AddOnSummaryDto(
            Count(AddOnStatus.Requested), Amount(AddOnStatus.Requested),
            Count(AddOnStatus.Agreed), Amount(AddOnStatus.Agreed),
            Count(AddOnStatus.Done), Amount(AddOnStatus.Done),
            Count(AddOnStatus.Declined));
    }

    public async Task<AddOnRequestDto> CreateAsync(SaveAddOnRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        await EnsureReferencesAsync(req, ct);

        var addOn = new AddOnRequest
        {
            TxnNumber = await sequences.NextAsync("ADD", ct),
            ProjectId = req.ProjectId,
            Date = req.Date,
            ExpenseHeadId = req.ExpenseHeadId,
            Description = req.Description.Trim(),
            ExpectedAmount = req.ExpectedAmount,
            Remarks = req.Remarks,
            Status = AddOnStatus.Requested
        };
        db.AddOnRequests.Add(addOn);
        await db.SaveChangesAsync(ct);

        return await db.AddOnRequests.AsNoTracking().Where(a => a.Id == addOn.Id).Select(Projection).FirstAsync(ct);
    }

    public async Task<AddOnRequestDto> UpdateAsync(Guid id, SaveAddOnRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var addOn = await db.AddOnRequests.FirstOrDefaultAsync(a => a.Id == id, ct)
                    ?? throw new NotFoundException("AddOnRequest", id);
        await EnsureReferencesAsync(req, ct);

        // The project is not editable: a change belongs to the villa it was asked about, and moving
        // it to another one would be a different request on a different house.
        if (addOn.ProjectId != req.ProjectId)
            throw new AppException("A change request cannot be moved to another villa.", 400);

        addOn.Date = req.Date;
        addOn.ExpenseHeadId = req.ExpenseHeadId;
        addOn.Description = req.Description.Trim();
        addOn.ExpectedAmount = req.ExpectedAmount;
        addOn.Remarks = req.Remarks;
        Touch(addOn);
        await db.SaveChangesAsync(ct);

        return await db.AddOnRequests.AsNoTracking().Where(a => a.Id == id).Select(Projection).FirstAsync(ct);
    }

    public async Task<AddOnRequestDto> SetStatusAsync(Guid id, AddOnStatus status, string? remarks,
        CancellationToken ct = default)
    {
        var addOn = await db.AddOnRequests.FirstOrDefaultAsync(a => a.Id == id, ct)
                    ?? throw new NotFoundException("AddOnRequest", id);
        if (status == AddOnStatus.Declined && string.IsNullOrWhiteSpace(remarks) && string.IsNullOrWhiteSpace(addOn.Remarks))
            throw new AppException("Say why the change is not going ahead.", 400);

        addOn.Status = status;
        if (!string.IsNullOrWhiteSpace(remarks)) addOn.Remarks = remarks;
        Touch(addOn);
        await db.SaveChangesAsync(ct);

        return await db.AddOnRequests.AsNoTracking().Where(a => a.Id == id).Select(Projection).FirstAsync(ct);
    }

    private void Touch(AddOnRequest addOn)
    {
        addOn.ModifiedAt = clock.Now;
        addOn.ModifiedBy = currentUser.UserId;
    }

    private async Task EnsureReferencesAsync(SaveAddOnRequest req, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct))
            throw new NotFoundException("Project", req.ProjectId);
        if (!await db.ExpenseHeads.AnyAsync(h => h.Id == req.ExpenseHeadId, ct))
            throw new NotFoundException("ExpenseHead", req.ExpenseHeadId);
    }

    private static readonly Expression<Func<AddOnRequest, AddOnRequestDto>> Projection = a => new AddOnRequestDto(
        a.Id, a.TxnNumber, a.ProjectId, a.Project.Name, a.Date, a.ExpenseHeadId, a.Head.Name,
        a.Description, a.ExpectedAmount, a.Status, a.Remarks, a.CreatedAt);
}
