using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Employees;

public record EmployeeDto(Guid Id, string Code, string Name, string Phone, decimal MonthlySalary,
    DateOnly JoinDate, DateOnly? LeaveDate, string? Designation, string? Address, string? Notes,
    Guid? SiteId, string? SiteName, bool IsActive,
    decimal TotalPaid, decimal AdvanceOutstanding);

public record SaveEmployeeRequest(string? Code, string Name, string Phone, decimal MonthlySalary,
    DateOnly JoinDate, DateOnly? LeaveDate, string? Designation, string? Address, string? Notes,
    Guid? SiteId, bool IsActive);

/// <summary>Name, phone, salary and join date are required — the rest is optional detail.</summary>
public class SaveEmployeeValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeValidator()
    {
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Employee name is required.").MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^[0-9+\-\s()]{6,20}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.MonthlySalary).GreaterThan(0).WithMessage("Monthly salary is required.");
        RuleFor(x => x.JoinDate).NotEqual(default(DateOnly)).WithMessage("Join date is required.");
        RuleFor(x => x.LeaveDate).GreaterThanOrEqualTo(x => x.JoinDate)
            .When(x => x.LeaveDate.HasValue)
            .WithMessage("Leave date cannot be before the join date.");
    }
}

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(PageQuery page, bool? active, Guid? siteId, CancellationToken ct = default);
    Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(SaveEmployeeRequest req, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest req, CancellationToken ct = default);
}

public class EmployeeService(IAppDbContext db, IValidator<SaveEmployeeRequest> validator,
    ICodeGenerator codes) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> ListAsync(PageQuery page, bool? active, Guid? siteId, CancellationToken ct = default)
    {
        var q = db.Employees.AsNoTracking();
        if (active is not null) q = q.Where(e => e.IsActive == active);
        if (siteId is not null) q = q.Where(e => e.SiteId == siteId);
        var term = page.Q?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(e => e.Name.Contains(term) || e.Code.Contains(term) || e.Phone.Contains(term)
                             || (e.Designation != null && e.Designation.Contains(term)));

        return await q.OrderBy(e => e.Name).Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Employees.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Employee", id);

    public async Task<EmployeeDto> CreateAsync(SaveEmployeeRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var code = await codes.ResolveAsync(req.Code, CodePrefixes.Employee, ct);
        if (await db.Employees.AnyAsync(e => e.Code == code, ct))
            throw new AppException($"Employee code '{code}' already exists.", 409);
        await EnsureSiteAsync(req.SiteId, ct);

        var employee = new Employee();
        Apply(employee, req, code);
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        return await GetAsync(employee.Id, ct);
    }

    public async Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
                       ?? throw new NotFoundException("Employee", id);
        // An edit that omits the code keeps the one the employee already has.
        var code = string.IsNullOrWhiteSpace(req.Code) ? employee.Code : req.Code.Trim();
        if (await db.Employees.AnyAsync(e => e.Code == code && e.Id != id, ct))
            throw new AppException($"Employee code '{code}' already exists.", 409);
        await EnsureSiteAsync(req.SiteId, ct);

        Apply(employee, req, code);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task EnsureSiteAsync(Guid? siteId, CancellationToken ct)
    {
        if (siteId is { } id && !await db.Sites.AnyAsync(s => s.Id == id, ct))
            throw new NotFoundException("Site", id);
    }

    private static void Apply(Employee e, SaveEmployeeRequest req, string code)
    {
        e.Code = code;
        e.Name = req.Name.Trim();
        e.Phone = req.Phone.Trim();
        e.MonthlySalary = req.MonthlySalary;
        e.JoinDate = req.JoinDate;
        e.LeaveDate = req.LeaveDate;
        e.Designation = req.Designation?.Trim();
        e.Address = req.Address?.Trim();
        e.Notes = req.Notes?.Trim();
        e.SiteId = req.SiteId;
        e.IsActive = req.IsActive;
    }

    // Balances are derived from posted payments rather than stored, so they cannot drift out of
    // step with the ledger that produced them.
    private static readonly Expression<Func<Employee, EmployeeDto>> Projection = e => new EmployeeDto(
        e.Id, e.Code, e.Name, e.Phone, e.MonthlySalary, e.JoinDate, e.LeaveDate,
        e.Designation, e.Address, e.Notes, e.SiteId, e.Site != null ? e.Site.Name : null, e.IsActive,
        e.Payments.Where(p => p.Status == TransactionStatus.Posted).Sum(p => (decimal?)(p.Amount - p.AdvanceRecovered)) ?? 0m,
        (e.Payments.Where(p => p.Status == TransactionStatus.Posted && p.Kind == EmployeePaymentKind.Advance)
            .Sum(p => (decimal?)p.Amount) ?? 0m)
        - (e.Payments.Where(p => p.Status == TransactionStatus.Posted)
            .Sum(p => (decimal?)p.AdvanceRecovered) ?? 0m));
}
