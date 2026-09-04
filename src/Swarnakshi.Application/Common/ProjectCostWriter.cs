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

    /// <summary>
    /// Records material charged to a project.
    ///
    /// <para><paramref name="materialId"/> is what classifies the cost when the caller has no
    /// <paramref name="expenseHeadId"/> of its own: the head is the material's own category, so
    /// cement lands under Civil &amp; Structure and a pipe under Plumbing. A villa's cost-by-head
    /// then reads as the trade breakdown a builder actually thinks in. An explicit head still
    /// wins — a material request that names a work stage keeps it.</para>
    /// </summary>
    Task<ProjectExpense> WriteMaterialCostAsync(Guid projectId, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string sourceType, Guid sourceId, string? description,
        Guid? materialId = null, CancellationToken ct = default);
}

public class ProjectCostWriter(IAppDbContext db, ITransactionSequenceService sequences, ICurrentUser currentUser)
    : IProjectCostWriter
{
    private async Task<Guid> DefaultHeadAsync(CancellationToken ct)
        => await db.ExpenseHeads.Where(h => h.Name == "Miscellaneous").Select(h => h.Id).FirstOrDefaultAsync(ct)
           is var id && id != Guid.Empty
            ? id
            : await db.ExpenseHeads.OrderBy(h => h.SortOrder).Select(h => h.Id).FirstAsync(ct);

    /// <summary>
    /// The expense head for a material: the one named after its category, created on first use.
    ///
    /// <para>Material used to fall through to Miscellaneous whenever the caller named no head,
    /// which put cement next to sundry contractor money in a villa's breakdown — the totals were
    /// right and the split was useless. The category is the classification the material already
    /// carries, so use it rather than asking the person entering a delivery note to also pick a
    /// work stage.</para>
    ///
    /// <para>Heads are created rather than assumed because the seeded list is work stages (RCC,
    /// Plastering) and only some of them happen to share a name with a material category. Where one
    /// does — Plumbing, Electrical, Painting — this finds it and nothing new is added. Matching is
    /// case-insensitive on the name, and a tenant that renames a head simply gets a new one on the
    /// next posting rather than a silent mis-file.</para>
    /// </summary>
    private async Task<Guid> HeadForMaterialAsync(Guid materialId, CancellationToken ct)
    {
        var categoryName = await db.Materials
            .Where(m => m.Id == materialId)
            .Select(m => m.Subcategory.Category.Name)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(categoryName)) return await DefaultHeadAsync(ct);

        var existing = await db.ExpenseHeads
            .Where(h => h.Name.ToLower() == categoryName.ToLower())
            .Select(h => h.Id)
            .FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        // Sorted after the seeded stages, so the stage list a builder knows stays at the top of
        // every head dropdown and these accumulate below it in the order they are first used.
        var lastOrder = await db.ExpenseHeads.MaxAsync(h => (int?)h.SortOrder, ct) ?? 0;
        var head = new ExpenseHead { Name = categoryName, SortOrder = lastOrder + 1, IsActive = true };
        db.ExpenseHeads.Add(head);
        await db.SaveChangesAsync(ct);
        return head.Id;
    }

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

    public async Task<ProjectExpense> WriteMaterialCostAsync(Guid projectId, decimal amount, DateOnly date,
        Guid? expenseHeadId, Guid? expenseSubheadId, string sourceType, Guid sourceId, string? description,
        Guid? materialId = null, CancellationToken ct = default)
    {
        // The caller's head wins when it has one; otherwise the material classifies itself.
        var headId = expenseHeadId
            ?? (materialId is { } id ? await HeadForMaterialAsync(id, ct) : (Guid?)null);

        return await WriteAsync(projectId, ProjectExpenseType.Material, amount, date, headId, expenseSubheadId,
            description, sourceType, sourceId, ct);
    }
}
