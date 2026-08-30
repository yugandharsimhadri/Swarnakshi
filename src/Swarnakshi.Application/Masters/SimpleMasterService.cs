using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Masters;

public enum SimpleMasterKind
{
    Unit, MaterialCategory, MaterialSubcategory, ExpenseHead, ExpenseSubhead,
    LabourCategory, PaymentMethod, ProjectType
}

public record SaveSimpleMasterRequest(string Name, string? Code, Guid? ParentId, int SortOrder, bool IsActive);

public interface ISimpleMasterService
{
    Task<Guid> SaveAsync(SimpleMasterKind kind, Guid? id, SaveSimpleMasterRequest req, CancellationToken ct = default);
    Task DeleteAsync(SimpleMasterKind kind, Guid id, CancellationToken ct = default);
}

/// <summary>CRUD for the small name/active masters. Delete is blocked when the row is referenced.</summary>
public class SimpleMasterService(IAppDbContext db) : ISimpleMasterService
{
    public async Task<Guid> SaveAsync(SimpleMasterKind kind, Guid? id, SaveSimpleMasterRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.", 400);
        var name = req.Name.Trim();

        switch (kind)
        {
            case SimpleMasterKind.Unit:
            {
                var code = (req.Code ?? "").Trim().ToUpperInvariant();
                if (code.Length == 0) throw new AppException("Unit code is required.", 400);
                if (await db.Units.AnyAsync(u => u.Code == code && u.Id != (id ?? Guid.Empty), ct))
                    throw new AppException($"Unit code '{code}' already exists.", 409);
                var e = await GetOrNew(db.Units, id, ct);
                e.Code = code; e.Name = name; e.IsActive = req.IsActive;
                return await Persist(db.Units, e, id, ct);
            }
            case SimpleMasterKind.MaterialCategory:
            {
                var e = await GetOrNew(db.MaterialCategories, id, ct);
                e.Name = name; e.SortOrder = req.SortOrder; e.IsActive = req.IsActive;
                return await Persist(db.MaterialCategories, e, id, ct);
            }
            case SimpleMasterKind.MaterialSubcategory:
            {
                if (req.ParentId is not { } catId || !await db.MaterialCategories.AnyAsync(c => c.Id == catId, ct))
                    throw new AppException("A valid parent category is required.", 400);
                var e = await GetOrNew(db.MaterialSubcategories, id, ct);
                e.Name = name; e.MaterialCategoryId = catId; e.IsActive = req.IsActive;
                return await Persist(db.MaterialSubcategories, e, id, ct);
            }
            case SimpleMasterKind.ExpenseHead:
            {
                var e = await GetOrNew(db.ExpenseHeads, id, ct);
                e.Name = name; e.SortOrder = req.SortOrder; e.IsActive = req.IsActive;
                return await Persist(db.ExpenseHeads, e, id, ct);
            }
            case SimpleMasterKind.ExpenseSubhead:
            {
                if (req.ParentId is not { } headId || !await db.ExpenseHeads.AnyAsync(h => h.Id == headId, ct))
                    throw new AppException("A valid parent expense head is required.", 400);
                var e = await GetOrNew(db.ExpenseSubheads, id, ct);
                e.Name = name; e.ExpenseHeadId = headId; e.IsActive = req.IsActive;
                return await Persist(db.ExpenseSubheads, e, id, ct);
            }
            case SimpleMasterKind.LabourCategory:
            {
                var e = await GetOrNew(db.LabourCategories, id, ct);
                e.Name = name; e.IsActive = req.IsActive;
                return await Persist(db.LabourCategories, e, id, ct);
            }
            case SimpleMasterKind.PaymentMethod:
            {
                var e = await GetOrNew(db.PaymentMethods, id, ct);
                e.Name = name; e.IsActive = req.IsActive;
                return await Persist(db.PaymentMethods, e, id, ct);
            }
            case SimpleMasterKind.ProjectType:
            {
                var e = await GetOrNew(db.ProjectTypes, id, ct);
                e.Name = name; e.IsActive = req.IsActive;
                return await Persist(db.ProjectTypes, e, id, ct);
            }
            default:
                throw new AppException("Unknown master kind.", 400);
        }
    }

    public async Task DeleteAsync(SimpleMasterKind kind, Guid id, CancellationToken ct = default)
    {
        async Task Remove<T>(DbSet<T> set, Func<Task<bool>> inUse) where T : BaseEntity
        {
            var e = await set.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException(kind.ToString(), id);
            if (await inUse()) throw new AppException("This record is in use and cannot be deleted. Mark it inactive instead.", 409);
            set.Remove(e);
        }

        switch (kind)
        {
            case SimpleMasterKind.Unit:
                await Remove(db.Units, () => db.Materials.AnyAsync(m => m.UnitId == id || m.SecondaryUnitId == id, ct)); break;
            case SimpleMasterKind.MaterialCategory:
                await Remove(db.MaterialCategories, () => db.MaterialSubcategories.AnyAsync(s => s.MaterialCategoryId == id, ct)); break;
            case SimpleMasterKind.MaterialSubcategory:
                await Remove(db.MaterialSubcategories, () => db.Materials.AnyAsync(m => m.MaterialSubcategoryId == id, ct)); break;
            case SimpleMasterKind.ExpenseHead:
                await Remove(db.ExpenseHeads, () => db.ExpenseSubheads.AnyAsync(s => s.ExpenseHeadId == id, ct)); break;
            case SimpleMasterKind.ExpenseSubhead:
                await Remove(db.ExpenseSubheads, () => db.ProjectExpenses.AnyAsync(x => x.ExpenseSubheadId == id, ct)); break;
            case SimpleMasterKind.LabourCategory:
                await Remove(db.LabourCategories, () => db.LabourEntries.AnyAsync(x => x.LabourCategoryId == id, ct)); break;
            case SimpleMasterKind.PaymentMethod:
                await Remove(db.PaymentMethods, () => db.ContractorPayments.AnyAsync(x => x.PaymentMethodId == id, ct)); break;
            case SimpleMasterKind.ProjectType:
                await Remove(db.ProjectTypes, () => db.Projects.AnyAsync(x => x.ProjectTypeId == id, ct)); break;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<T> GetOrNew<T>(DbSet<T> set, Guid? id, CancellationToken ct) where T : BaseEntity, new()
        => id is null ? new T() : await set.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(typeof(T).Name, id);

    private async Task<Guid> Persist<T>(DbSet<T> set, T entity, Guid? id, CancellationToken ct) where T : BaseEntity
    {
        if (id is null) set.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
