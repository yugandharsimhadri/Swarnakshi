using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Expenses;

public record SiteExpenseDto(Guid Id, string TxnNumber, Guid SiteId, string SiteName, DateOnly Date,
    Guid ExpenseHeadId, string ExpenseHeadName, string? Description, decimal Amount,
    PaymentStatus PaymentStatus, Guid? PaymentMethodId, TransactionStatus Status);

public record SaveSiteExpenseRequest(Guid SiteId, DateOnly Date, Guid ExpenseHeadId,
    string? Description, decimal Amount, PaymentStatus PaymentStatus, Guid? PaymentMethodId);

public class SaveSiteExpenseValidator : AbstractValidator<SaveSiteExpenseRequest>
{
    public SaveSiteExpenseValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.ExpenseHeadId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Date is required.");
    }
}

public interface ISiteExpenseService
{
    Task<PagedResult<SiteExpenseDto>> ListAsync(PageQuery page, Guid? siteId, DateOnly? from, DateOnly? to,
        CancellationToken ct = default);
    Task<SiteExpenseDto> CreateAsync(SaveSiteExpenseRequest req, CancellationToken ct = default);
    Task<SiteExpenseDto> CancelAsync(Guid id, string reason, CancellationToken ct = default);
    /// <summary>Site overhead per site, for the reports that have to add it back in.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> TotalsBySiteAsync(CancellationToken ct = default);
}

/// <summary>
/// Costs that belong to a site rather than to any one villa — the watchman, temporary power, the
/// site office, a supervisor's salary.
///
/// Before this existed such a cost had nowhere to go: everything had to attach to a villa, so it
/// was either dumped on whichever villa was handy (making that villa look expensive and the others
/// cheap) or never recorded at all. Both are worse than a separate bucket.
/// </summary>
public class SiteExpenseService(
    IAppDbContext db,
    ITransactionSequenceService sequences,
    IValidator<SaveSiteExpenseRequest> validator) : ISiteExpenseService
{
    public async Task<PagedResult<SiteExpenseDto>> ListAsync(PageQuery page, Guid? siteId,
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = db.SiteExpenses.AsNoTracking();
        if (siteId is not null) q = q.Where(e => e.SiteId == siteId);
        if (from is not null) q = q.Where(e => e.Date >= from);
        if (to is not null) q = q.Where(e => e.Date <= to);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(e => e.TxnNumber.Contains(page.Q) || (e.Description != null && e.Description.Contains(page.Q)));

        return await q.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<SiteExpenseDto> CreateAsync(SaveSiteExpenseRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Sites.AnyAsync(s => s.Id == req.SiteId, ct))
            throw new NotFoundException("Site", req.SiteId);
        if (!await db.ExpenseHeads.AnyAsync(h => h.Id == req.ExpenseHeadId, ct))
            throw new NotFoundException("ExpenseHead", req.ExpenseHeadId);
        if (req.PaymentMethodId is { } pm && !await db.PaymentMethods.AnyAsync(m => m.Id == pm, ct))
            throw new NotFoundException("PaymentMethod", pm);

        var entity = new SiteExpense
        {
            TxnNumber = await sequences.NextAsync("SITEEXP", ct),
            SiteId = req.SiteId, Date = req.Date, ExpenseHeadId = req.ExpenseHeadId,
            Description = req.Description, Amount = req.Amount,
            PaymentStatus = req.PaymentStatus, PaymentMethodId = req.PaymentMethodId,
            Status = TransactionStatus.Posted,
        };
        db.SiteExpenses.Add(entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<SiteExpenseDto> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var entity = await db.SiteExpenses.FirstOrDefaultAsync(e => e.Id == id, ct)
                     ?? throw new NotFoundException("SiteExpense", id);
        if (entity.Status == TransactionStatus.Cancelled)
            throw new AppException("This expense is already cancelled.", 409);

        // Never deleted — cancelled, with the reason kept, so the trail survives.
        entity.Status = TransactionStatus.Cancelled;
        entity.Remarks = reason;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> TotalsBySiteAsync(CancellationToken ct = default)
        => await db.SiteExpenses.AsNoTracking()
            .Where(e => e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.SiteId)
            .Select(g => new { SiteId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SiteId, x => x.Total, ct);

    private async Task<SiteExpenseDto> GetAsync(Guid id, CancellationToken ct)
        => await db.SiteExpenses.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("SiteExpense", id);

    private static readonly System.Linq.Expressions.Expression<Func<SiteExpense, SiteExpenseDto>> Projection =
        e => new SiteExpenseDto(e.Id, e.TxnNumber, e.SiteId, e.Site.Name, e.Date,
            e.ExpenseHeadId, e.Head.Name, e.Description, e.Amount,
            e.PaymentStatus, e.PaymentMethodId, e.Status);
}
