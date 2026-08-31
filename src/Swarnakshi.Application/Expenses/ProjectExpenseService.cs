using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Expenses;

public record ProjectExpenseDto(Guid Id, string TxnNumber, Guid ProjectId, string ProjectName, DateOnly Date,
    Guid ExpenseHeadId, string ExpenseHeadName, Guid? ExpenseSubheadId, string? ExpenseSubheadName,
    string? Description, decimal Amount, ProjectExpenseType ExpenseType, PaymentStatus PaymentStatus,
    string? SourceType, TransactionStatus Status);

public record SaveProjectExpenseRequest(Guid ProjectId, DateOnly Date, Guid ExpenseHeadId, Guid? ExpenseSubheadId,
    string? Description, decimal Amount, ProjectExpenseType ExpenseType, PaymentStatus PaymentStatus, Guid? PaymentMethodId);

public class SaveProjectExpenseValidator : AbstractValidator<SaveProjectExpenseRequest>
{
    public SaveProjectExpenseValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ExpenseHeadId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseType)
            .Must(t => t is not (ProjectExpenseType.Contractor or ProjectExpenseType.Labour))
            .WithMessage("Contractor and Labour costs are recorded through their own screens, not as a direct expense.");
    }
}

public record CostByHead(Guid ExpenseHeadId, string ExpenseHeadName, decimal Amount);

public interface IProjectExpenseService
{
    Task<PagedResult<ProjectExpenseDto>> ListAsync(PageQuery page, Guid? projectId, Guid? expenseHeadId,
        ProjectExpenseType? type, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<ProjectExpenseDto> CreateAsync(SaveProjectExpenseRequest req, CancellationToken ct = default);
    Task<ProjectExpenseDto> CancelAsync(Guid id, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<CostByHead>> CostByHeadAsync(Guid projectId, CancellationToken ct = default);
}

public class ProjectExpenseService(
    IAppDbContext db, ICurrentUser currentUser, ITransactionSequenceService sequences,
    IValidator<SaveProjectExpenseRequest> validator) : IProjectExpenseService
{
    public async Task<PagedResult<ProjectExpenseDto>> ListAsync(PageQuery page, Guid? projectId, Guid? expenseHeadId,
        ProjectExpenseType? type, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = db.ProjectExpenses.AsNoTracking();
        if (projectId is not null) q = q.Where(e => e.ProjectId == projectId);
        if (expenseHeadId is not null) q = q.Where(e => e.ExpenseHeadId == expenseHeadId);
        if (type is not null) q = q.Where(e => e.ExpenseType == type);
        if (from is not null) q = q.Where(e => e.Date >= from);
        if (to is not null) q = q.Where(e => e.Date <= to);
        return await q.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<ProjectExpenseDto> CreateAsync(SaveProjectExpenseRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct))
            throw new NotFoundException("Project", req.ProjectId);
        if (!await db.ExpenseHeads.AnyAsync(h => h.Id == req.ExpenseHeadId, ct))
            throw new NotFoundException("ExpenseHead", req.ExpenseHeadId);
        if (req.ExpenseSubheadId is { } sid && !await db.ExpenseSubheads.AnyAsync(s => s.Id == sid && s.ExpenseHeadId == req.ExpenseHeadId, ct))
            throw new AppException("Subhead does not belong to the selected head.", 400);

        var expense = new ProjectExpense
        {
            TxnNumber = await sequences.NextAsync("EXP", ct),
            ProjectId = req.ProjectId, Date = req.Date, ExpenseHeadId = req.ExpenseHeadId,
            ExpenseSubheadId = req.ExpenseSubheadId, Description = req.Description, Amount = req.Amount,
            ExpenseType = req.ExpenseType, PaymentStatus = req.PaymentStatus, PaymentMethodId = req.PaymentMethodId,
            SourceType = "Manual", Status = TransactionStatus.Posted,
            ApprovedBy = currentUser.UserId, ApprovedAt = DateTimeOffset.UtcNow
        };
        db.ProjectExpenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return await db.ProjectExpenses.AsNoTracking().Where(e => e.Id == expense.Id).Select(Projection).FirstAsync(ct);
    }

    public async Task<ProjectExpenseDto> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var expense = await db.ProjectExpenses.FirstOrDefaultAsync(e => e.Id == id, ct)
                      ?? throw new NotFoundException("ProjectExpense", id);
        if (expense.SourceType != "Manual")
            throw new AppException("Only manually-entered expenses can be cancelled here. Reverse the source document instead.", 409);
        if (expense.Status == TransactionStatus.Cancelled)
            throw new AppException("Already cancelled.", 409);
        expense.Status = TransactionStatus.Cancelled;
        expense.Remarks = reason;
        expense.Amount = 0m; // keeps the row for audit but removes it from cost roll-ups
        await db.SaveChangesAsync(ct);
        return await db.ProjectExpenses.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstAsync(ct);
    }

    public async Task<IReadOnlyList<CostByHead>> CostByHeadAsync(Guid projectId, CancellationToken ct = default)
    {
        var grouped = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.ExpenseHeadId)
            .Select(g => new { HeadId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var names = await db.ExpenseHeads.AsNoTracking()
            .Where(h => grouped.Select(g => g.HeadId).Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Name, ct);

        return grouped
            .Select(g => new CostByHead(g.HeadId, names.GetValueOrDefault(g.HeadId, "—"), g.Amount))
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    private static readonly Expression<Func<ProjectExpense, ProjectExpenseDto>> Projection = e => new ProjectExpenseDto(
        e.Id, e.TxnNumber, e.ProjectId, e.Project.Name, e.Date, e.ExpenseHeadId, e.Head.Name,
        e.ExpenseSubheadId, e.Subhead != null ? e.Subhead.Name : null, e.Description, e.Amount,
        e.ExpenseType, e.PaymentStatus, e.SourceType, e.Status);
}
