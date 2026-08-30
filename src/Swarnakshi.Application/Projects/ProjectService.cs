using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Projects;

public record ProjectDto(Guid Id, string Code, string Name, string? VillaNumber, Guid SiteId, string SiteName,
    Guid? CustomerId, string? CustomerName, Guid? ProjectTypeId, DateOnly? StartDate,
    DateOnly? ExpectedCompletionDate, decimal EstimatedCost, decimal? ContractSaleValue,
    ProjectStatus Status, string? Notes);

public record ProjectFinancialSummary(
    Guid ProjectId, string Name, decimal EstimatedCost, decimal? ContractSaleValue,
    decimal MaterialCost, decimal LabourCost, decimal ContractorCost, decimal OtherCost,
    decimal TotalCost, decimal CustomerReceived, decimal CustomerOutstanding,
    decimal BudgetVariance, decimal? Margin);

public record SaveProjectRequest(string Code, string Name, string? VillaNumber, Guid SiteId,
    Guid? CustomerId, Guid? ProjectTypeId, string? Address, DateOnly? StartDate,
    DateOnly? ExpectedCompletionDate, DateOnly? ActualCompletionDate, decimal EstimatedCost,
    decimal? ContractSaleValue, ProjectStatus Status, string? Notes);

public class SaveProjectValidator : AbstractValidator<SaveProjectRequest>
{
    public SaveProjectValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ContractSaleValue).GreaterThanOrEqualTo(0).When(x => x.ContractSaleValue.HasValue);
    }
}

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> ListAsync(PageQuery page, Guid? siteId, ProjectStatus? status, Guid? customerId, CancellationToken ct = default);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(SaveProjectRequest req, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, SaveProjectRequest req, CancellationToken ct = default);
    Task<ProjectFinancialSummary> SummaryAsync(Guid id, CancellationToken ct = default);
}

public class ProjectService(IAppDbContext db, IValidator<SaveProjectRequest> validator) : IProjectService
{
    public async Task<PagedResult<ProjectDto>> ListAsync(PageQuery page, Guid? siteId, ProjectStatus? status, Guid? customerId, CancellationToken ct = default)
    {
        var q = db.Projects.AsNoTracking();
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        if (status is not null) q = q.Where(p => p.Status == status);
        if (customerId is not null) q = q.Where(p => p.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(p => p.Name.Contains(page.Q) || p.Code.Contains(page.Q) || (p.VillaNumber != null && p.VillaNumber.Contains(page.Q)));
        return await q.OrderBy(p => p.Name).Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Projects.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Project", id);

    public async Task<ProjectDto> CreateAsync(SaveProjectRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        await EnsureRefsAsync(req, ct);
        var code = req.Code.Trim();
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
        var code = req.Code.Trim();
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

    public async Task<ProjectFinancialSummary> SummaryAsync(Guid id, CancellationToken ct = default)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Project", id);

        var expenses = db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == id && e.Status == TransactionStatus.Posted);

        var material = await expenses.Where(e => e.ExpenseType == ProjectExpenseType.Material).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var labour = await expenses.Where(e => e.ExpenseType == ProjectExpenseType.Labour).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var other = await expenses.Where(e => e.ExpenseType != ProjectExpenseType.Material
            && e.ExpenseType != ProjectExpenseType.Labour && e.ExpenseType != ProjectExpenseType.Contractor)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var contractor = await db.ContractorPayments.AsNoTracking()
            .Where(cp => cp.ProjectId == id && cp.Status == TransactionStatus.Posted)
            .SumAsync(cp => (decimal?)cp.Amount, ct) ?? 0m;

        var received = await db.CustomerPayments.AsNoTracking()
            .Where(cp => cp.ProjectId == id && cp.Status == TransactionStatus.Posted)
            .SumAsync(cp => (decimal?)cp.Amount, ct) ?? 0m;

        var total = material + labour + contractor + other;
        var sale = p.ContractSaleValue;
        return new ProjectFinancialSummary(p.Id, p.Name, p.EstimatedCost, sale,
            material, labour, contractor, other, total,
            received, (sale ?? 0m) - received,
            p.EstimatedCost - total, sale.HasValue ? sale - total : null);
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
    }

    private static readonly System.Linq.Expressions.Expression<Func<Project, ProjectDto>> Projection =
        p => new ProjectDto(p.Id, p.Code, p.Name, p.VillaNumber, p.SiteId, p.Site.Name,
            p.CustomerId, p.Customer != null ? p.Customer.Name : null, p.ProjectTypeId,
            p.StartDate, p.ExpectedCompletionDate, p.EstimatedCost, p.ContractSaleValue, p.Status, p.Notes);
}
