using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Masters;

// ---- DTOs ----------------------------------------------------------------
public record LookupDto(Guid Id, string Name, bool IsActive);
public record UnitDto(Guid Id, string Code, string Name, bool IsActive);
public record CategoryDto(Guid Id, string Name, int SortOrder, bool IsActive);
public record SubcategoryDto(Guid Id, Guid ParentId, string ParentName, string Name, bool IsActive);

public enum PartyKind { Contractor, Customer, Supplier }

// ---- Service -----------------------------------------------------------
public interface IMasterService
{
    Task<IReadOnlyList<UnitDto>> UnitsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> MaterialCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubcategoryDto>> MaterialSubcategoriesAsync(Guid? categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryDto>> ExpenseHeadsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubcategoryDto>> ExpenseSubheadsAsync(Guid? headId, CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> LabourCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> PaymentMethodsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> ProjectTypesAsync(CancellationToken ct = default);


}

public class MasterService(IAppDbContext db) : IMasterService
{
    public async Task<IReadOnlyList<UnitDto>> UnitsAsync(CancellationToken ct = default)
        => await db.Units.AsNoTracking().OrderBy(u => u.Name)
            .Select(u => new UnitDto(u.Id, u.Code, u.Name, u.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<CategoryDto>> MaterialCategoriesAsync(CancellationToken ct = default)
        => await db.MaterialCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder, c.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<SubcategoryDto>> MaterialSubcategoriesAsync(Guid? categoryId, CancellationToken ct = default)
    {
        var q = db.MaterialSubcategories.AsNoTracking().AsQueryable();
        if (categoryId is not null) q = q.Where(s => s.MaterialCategoryId == categoryId);
        return await q.OrderBy(s => s.Name)
            .Select(s => new SubcategoryDto(s.Id, s.MaterialCategoryId, s.Category.Name, s.Name, s.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryDto>> ExpenseHeadsAsync(CancellationToken ct = default)
        => await db.ExpenseHeads.AsNoTracking().OrderBy(h => h.SortOrder).ThenBy(h => h.Name)
            .Select(h => new CategoryDto(h.Id, h.Name, h.SortOrder, h.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<SubcategoryDto>> ExpenseSubheadsAsync(Guid? headId, CancellationToken ct = default)
    {
        var q = db.ExpenseSubheads.AsNoTracking().AsQueryable();
        if (headId is not null) q = q.Where(s => s.ExpenseHeadId == headId);
        return await q.OrderBy(s => s.Name)
            .Select(s => new SubcategoryDto(s.Id, s.ExpenseHeadId, s.Head.Name, s.Name, s.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LookupDto>> LabourCategoriesAsync(CancellationToken ct = default)
        => await db.LabourCategories.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new LookupDto(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> PaymentMethodsAsync(CancellationToken ct = default)
        => await db.PaymentMethods.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new LookupDto(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> ProjectTypesAsync(CancellationToken ct = default)
        => await db.ProjectTypes.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new LookupDto(x.Id, x.Name, x.IsActive)).ToListAsync(ct);

}
