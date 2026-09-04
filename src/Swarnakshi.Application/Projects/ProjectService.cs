using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Projects;

/// <summary>
/// <para><see cref="SpentCost"/> and <see cref="BurnPercent"/> are filled after projection — see
/// <see cref="ProjectService.WithSpendAsync"/>. They are on the list row because the owner scans
/// the villa list, not ten villa screens, and a villa running hot has to be visible from there.</para>
/// </summary>
public record ProjectDto(Guid Id, string Code, string Name, string? VillaNumber, Guid SiteId, string SiteName,
    Guid? CustomerId, string? CustomerName, Guid? ProjectTypeId, DateOnly? StartDate,
    DateOnly? ExpectedCompletionDate, decimal EstimatedCost, decimal? ContractSaleValue,
    ProjectStatus Status, int CompletionPercent, string? Notes,
    decimal SpentCost = 0m, decimal? BurnPercent = null);

/// <summary>
/// How the book of work is spread across its stages, plus the average completion of what is under
/// way. Cancelled is reported separately rather than folded into a bucket: a cancelled villa is not
/// "not started", and counting it as such would quietly overstate the work still to come.
/// </summary>
public record ProjectProgressSummary(
    int Total, int NotStarted, int InProgress, int Completed, int OnHold, int Cancelled,
    int AverageCompletionOfInProgress);

/// <summary>
/// A villa's money, told honestly.
///
/// <para><see cref="Margin"/> used to be sale price minus cost so far, which on a half-built villa
/// credits the whole sale value against half the cost and reports a profit nobody has earned.
/// <see cref="EarnedRevenue"/> recognises the sale in proportion to how much has actually been
/// built, and <see cref="EarnedMargin"/> is the number to trust. The contracted value stays on the
/// record because it is the right figure for the sales pipeline — just not for profit.</para>
///
/// <para><see cref="BurnPercent"/> compares what has been spent with what the estimate says should
/// have been spent by this stage. <see cref="BudgetVariance"/> alone shows a big positive on every
/// unfinished villa, which reads as money saved when it is really a house that is not finished.</para>
///
/// <para><see cref="CommittedContractorCost"/> is money promised under open work orders and not yet
/// paid. It is not in <see cref="TotalCost"/> — nothing has left the bank — but it is owed, so
/// <see cref="CommittedTotalCost"/> is the figure that answers "what will finishing this cost".</para>
/// </summary>
public record ProjectFinancialSummary(
    Guid ProjectId, string Name, decimal EstimatedCost, decimal? ContractSaleValue,
    decimal MaterialCost, decimal LabourCost, decimal ContractorCost, decimal OtherCost,
    decimal TotalCost, decimal CustomerReceived, decimal CustomerOutstanding,
    decimal BudgetVariance, decimal? Margin,
    int CompletionPercent, decimal? EarnedRevenue, decimal? EarnedMargin,
    decimal CommittedContractorCost, decimal CommittedTotalCost,
    decimal ExpectedCostToDate, decimal? BurnPercent, bool DuesOnHandover);

/// <summary>Code is optional — leave it null and one is minted. See <see cref="ICodeGenerator"/>.</summary>
public record SaveProjectRequest(string? Code, string Name, string? VillaNumber, Guid SiteId,
    Guid? CustomerId, Guid? ProjectTypeId, string? Address, DateOnly? StartDate,
    DateOnly? ExpectedCompletionDate, DateOnly? ActualCompletionDate, decimal EstimatedCost,
    decimal? ContractSaleValue, ProjectStatus Status, int CompletionPercent, string? Notes);

public class SaveProjectValidator : AbstractValidator<SaveProjectRequest>
{
    public SaveProjectValidator()
    {
        RuleFor(x => x.Code).MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContractSaleValue).GreaterThanOrEqualTo(0).When(x => x.ContractSaleValue.HasValue);
        RuleFor(x => x.CompletionPercent).InclusiveBetween(0, 100);

        // A project that has not started cannot be part-built. Rejected rather than quietly
        // corrected, because the fix is a decision only the user can make: if there is progress to
        // report, the project is under way and its status should say so.
        RuleFor(x => x.CompletionPercent)
            .Equal(0)
            .When(x => x.Status == ProjectStatus.Planned)
            .WithMessage("A project that has not started yet cannot report progress. "
                + "Set the status to Active first.");
    }
}

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> ListAsync(PageQuery page, Guid? siteId, ProjectStatus? status, Guid? customerId, CancellationToken ct = default);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(SaveProjectRequest req, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest req, CancellationToken ct = default);
    Task<ProjectFinancialSummary> SummaryAsync(Guid id, CancellationToken ct = default);
    Task<ProjectProgressSummary> ProgressSummaryAsync(Guid? siteId, CancellationToken ct = default);
}

public class ProjectService(IAppDbContext db, IValidator<SaveProjectRequest> validator, ICodeGenerator codes) : IProjectService
{
    public async Task<PagedResult<ProjectDto>> ListAsync(PageQuery page, Guid? siteId, ProjectStatus? status, Guid? customerId, CancellationToken ct = default)
    {
        var q = db.Projects.AsNoTracking();
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        if (status is not null) q = q.Where(p => p.Status == status);
        if (customerId is not null) q = q.Where(p => p.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(p => p.Name.Contains(page.Q) || p.Code.Contains(page.Q) || (p.VillaNumber != null && p.VillaNumber.Contains(page.Q)));
        var paged = await q.OrderBy(p => p.Name).Select(Projection).ToPagedAsync(page, ct);
        return new PagedResult<ProjectDto>
        {
            Items = await WithSpendAsync(paged.Items, ct),
            Page = paged.Page, PageSize = paged.PageSize, Total = paged.Total,
        };
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await db.Projects.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Project", id);
        return (await WithSpendAsync([dto], ct))[0];
    }

    /// <summary>
    /// Fills in spend and burn for a page of rows with one grouped query, rather than a correlated
    /// subquery per row. Burn is spend against what the estimate says should have gone by this
    /// stage; a villa with nothing built has no burn, because dividing by nothing built is not an
    /// overrun.
    /// </summary>
    private async Task<IReadOnlyList<ProjectDto>> WithSpendAsync(IReadOnlyList<ProjectDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.Id).ToList();

        var costs = await db.ProjectExpenses.AsNoTracking()
            .Where(e => ids.Contains(e.ProjectId) && e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.ProjectId)
            .Select(g => new { ProjectId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Total, ct);

        return rows.Select(r =>
        {
            var spent = costs.GetValueOrDefault(r.Id, 0m);
            var expected = Math.Round(r.EstimatedCost * r.CompletionPercent / 100m, 2);
            return r with
            {
                SpentCost = spent,
                BurnPercent = expected > 0 ? Math.Round(spent / expected * 100m, 0) : null,
            };
        }).ToList();
    }

    public async Task<ProjectDto> CreateAsync(SaveProjectRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        await EnsureRefsAsync(req, ct);
        var code = await codes.ResolveAsync(req.Code, CodePrefixes.Project, ct);
        if (await db.Projects.AnyAsync(p => p.Code == code, ct))
            throw new AppException($"Project code '{code}' already exists.", 409);

        var p = new Project();
        Apply(p, req, code);
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Project", id);
        // An edit that omits the code keeps the one the project already has — the screens no longer show it.
        var code = string.IsNullOrWhiteSpace(req.Code) ? p.Code : req.Code.Trim();
        if (await db.Projects.AnyAsync(x => x.Code == code && x.Id != id, ct))
            throw new AppException($"Project code '{code}' already exists.", 409);

        // Site change blocked once inventory activity exists for this project.
        if (p.SiteId != req.SiteId &&
            await db.InventoryTransactions.AnyAsync(t => t.ProjectId == id, ct))
            throw new AppException("Cannot change site: this project already has inventory activity.", 409);

        await EnsureRefsAsync(req, ct);
        Apply(p, req, code);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ProjectProgressSummary> ProgressSummaryAsync(Guid? siteId, CancellationToken ct = default)
    {
        var q = db.Projects.AsNoTracking();
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);

        // One grouped round trip rather than six counts: the buckets are shares of the same set, and
        // reading them from separate queries lets them disagree if anything changes between.
        var byStatus = await q
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(ProjectStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var inProgressStatuses = new[] { ProjectStatus.Active, ProjectStatus.OnHold };
        var average = await q.Where(p => inProgressStatuses.Contains(p.Status))
            .Select(p => (double?)p.CompletionPercent).AverageAsync(ct) ?? 0d;

        var active = CountOf(ProjectStatus.Active);
        var onHold = CountOf(ProjectStatus.OnHold);

        return new ProjectProgressSummary(
            Total: byStatus.Sum(x => x.Count),
            NotStarted: CountOf(ProjectStatus.Planned),
            // On hold is work that has started and stopped, so it belongs to what is under way
            // rather than to what has not begun — and it is also reported on its own below.
            InProgress: active + onHold,
            Completed: CountOf(ProjectStatus.Completed),
            OnHold: onHold,
            Cancelled: CountOf(ProjectStatus.Cancelled),
            AverageCompletionOfInProgress: (int)Math.Round(average));
    }

    public async Task<ProjectFinancialSummary> SummaryAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Project", id);

        var expenses = db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == id && e.Status == TransactionStatus.Posted);

        // Every costed event (consumption, labour, contractor payment, manual expense) writes exactly one
        // posted ProjectExpense row, so summing by type here cannot double count.
        var material = await expenses.Where(e => e.ExpenseType == ProjectExpenseType.Material).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var labour = await expenses.Where(e => e.ExpenseType == ProjectExpenseType.Labour).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var contractor = await expenses.Where(e => e.ExpenseType == ProjectExpenseType.Contractor).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var other = await expenses.Where(e => e.ExpenseType != ProjectExpenseType.Material
            && e.ExpenseType != ProjectExpenseType.Labour && e.ExpenseType != ProjectExpenseType.Contractor)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var received = await db.CustomerPayments.AsNoTracking()
            .Where(cp => cp.ProjectId == id && cp.Status == TransactionStatus.Posted)
            .SumAsync(cp => (decimal?)cp.Amount, ct) ?? 0m;

        // Open work orders: promised, not yet paid, and therefore not in TotalCost.
        var committed = await db.ContractWorks.AsNoTracking()
            .Where(w => w.ProjectId == id && w.WorkStatus != ContractWorkStatus.Cancelled)
            .SumAsync(w => (decimal?)w.Balance, ct) ?? 0m;

        var total = material + labour + contractor + other;
        var sale = p.ContractSaleValue;
        var earned = sale.HasValue ? Math.Round(sale.Value * p.CompletionPercent / 100m, 2) : (decimal?)null;
        // Returned as well as used: the screen shows "spent X against Y expected", and Y is this.
        // The UI used to recompute it from EstimatedCost and CompletionPercent, which meant the
        // rule for what "expected by now" means lived in two places and could drift.
        var expectedByNow = Math.Round(p.EstimatedCost * p.CompletionPercent / 100m, 2);
        // Nothing built yet means nothing to compare against — an unstarted villa is not "over budget".
        var burn = expectedByNow > 0 ? Math.Round(total / expectedByNow * 100m, 0) : (decimal?)null;
        var outstanding = (sale ?? 0m) - received;

        return new ProjectFinancialSummary(p.Id, p.Name, p.EstimatedCost, sale,
            material, labour, contractor, other, total,
            received, outstanding,
            p.EstimatedCost - total, sale.HasValue ? sale - total : null,
            p.CompletionPercent, earned, earned.HasValue ? earned.Value - total : null,
            committed, total + committed, expectedByNow, burn,
            p.Status == ProjectStatus.Completed && outstanding > 0);
    }

    private async Task EnsureRefsAsync(SaveProjectRequest req, CancellationToken ct)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == req.SiteId, ct))
            throw new NotFoundException("Site", req.SiteId);
        if (req.CustomerId is { } cid && !await db.Customers.AnyAsync(c => c.Id == cid && c.IsActive, ct))
            throw new AppException("Customer not found or inactive.", 400);
        if (req.ProjectTypeId is { } tid && !await db.ProjectTypes.AnyAsync(t => t.Id == tid, ct))
            throw new NotFoundException("ProjectType", tid);
    }

    private static void Apply(Project p, SaveProjectRequest req, string code)
    {
        p.Code = code; p.Name = req.Name.Trim(); p.VillaNumber = req.VillaNumber; p.SiteId = req.SiteId;
        p.CustomerId = req.CustomerId; p.ProjectTypeId = req.ProjectTypeId; p.Address = req.Address;
        p.StartDate = req.StartDate; p.ExpectedCompletionDate = req.ExpectedCompletionDate;
        p.ActualCompletionDate = req.ActualCompletionDate; p.EstimatedCost = req.EstimatedCost;
        p.ContractSaleValue = req.ContractSaleValue; p.Status = req.Status; p.Notes = req.Notes;

        // Completing a project settles its percentage: leaving a finished villa reading 90% would
        // make the average of what is under way permanently wrong.
        p.CompletionPercent = req.Status == ProjectStatus.Completed ? 100 : req.CompletionPercent;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Project, ProjectDto>> Projection =
        p => new ProjectDto(p.Id, p.Code, p.Name, p.VillaNumber, p.SiteId, p.Site.Name,
            p.CustomerId, p.Customer != null ? p.Customer.Name : null, p.ProjectTypeId,
            p.StartDate, p.ExpectedCompletionDate, p.EstimatedCost, p.ContractSaleValue, p.Status,
            p.CompletionPercent, p.Notes);
}
