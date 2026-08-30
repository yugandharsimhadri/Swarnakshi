using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Common;

/// <summary>Writes posted ProjectExpense rows — the single place project cost is recorded, so nothing is double counted.</summary>
public interface IProjectCostWriter
{
    Task<ProjectExpense> WriteAsync(Guid projectId, ProjectExpenseType type, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string? description, string sourceType, Guid sourceId,
        CancellationToken ct = default);

    Task<ProjectExpense> WriteMaterialCostAsync(Guid projectId, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string sourceType, Guid sourceId, string? description,
        CancellationToken ct = default);
}

public class ProjectCostWriter(IAppDbContext db, ITransactionSequenceService sequences, ICurrentUser currentUser)
    : IProjectCostWriter
{
    private async Task<Guid> DefaultHeadAsync(CancellationToken ct)
        => await db.ExpenseHeads.Where(h => h.Name == "Miscellaneous").Select(h => h.Id).FirstOrDefaultAsync(ct)
           is var id && id != Guid.Empty
            ? id
            : await db.ExpenseHeads.OrderBy(h => h.SortOrder).Select(h => h.Id).FirstAsync(ct);

    public async Task<ProjectExpense> WriteAsync(Guid projectId, ProjectExpenseType type, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string? description, string sourceType, Guid sourceId,
        CancellationToken ct = default)
    {
        var headId = expenseHeadId ?? await DefaultHeadAsync(ct);
        var expense = new ProjectExpense
        {
            TxnNumber = await sequences.NextAsync("EXP", ct),
            ProjectId = projectId, Date = date, ExpenseHeadId = headId, ExpenseSubheadId = expenseSubheadId,
            Description = description, Amount = amount, ExpenseType = type,
            PaymentStatus = PaymentStatus.Paid, SourceType = sourceType, SourceId = sourceId,
            Status = TransactionStatus.Posted, ApprovedBy = currentUser.UserId, ApprovedAt = DateTimeOffset.UtcNow
        };
        db.ProjectExpenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return expense;
    }

    public Task<ProjectExpense> WriteMaterialCostAsync(Guid projectId, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string sourceType, Guid sourceId, string? description,
        CancellationToken ct = default)
        => WriteAsync(projectId, ProjectExpenseType.Material, amount, date, expenseHeadId, expenseSubheadId,
            description, sourceType, sourceId, ct);
}
