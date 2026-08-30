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
public record MaterialDto(Guid Id, string Code, string Name, Guid MaterialSubcategoryId, string SubcategoryName,
    string CategoryName, Guid UnitId, string UnitCode, decimal MinStockLevel, decimal ReorderLevel,
    decimal DefaultPurchaseRate, decimal? GstRate, bool IsActive, string? Description, string? Notes);
public record PartyDto(Guid Id, string Code, string Name, string? CompanyName, string? Mobile, string? Email,
    string? Address, string? Pan, string? Gstin, string? Type, bool IsActive, string? Notes);

public record SaveMaterialRequest(string Code, string Name, Guid MaterialSubcategoryId, Guid UnitId,
    Guid? SecondaryUnitId, decimal? ConversionFactor, decimal MinStockLevel, decimal ReorderLevel,
    decimal DefaultPurchaseRate, decimal? GstRate, bool IsActive, string? Description, string? Notes);

public record SavePartyRequest(string Code, string Name, string? CompanyName, string? Mobile, string? Email,
    string? Address, string? Pan, string? Gstin, string? Type, bool IsActive, string? Notes);

public class SaveMaterialValidator : AbstractValidator<SaveMaterialRequest>
{
    public SaveMaterialValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialSubcategoryId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.DefaultPurchaseRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStockLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

public class SavePartyValidator : AbstractValidator<SavePartyRequest>
{
    public SavePartyValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

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

    Task<PagedResult<MaterialDto>> MaterialsAsync(PageQuery page, Guid? categoryId, bool? active, CancellationToken ct = default);
    Task<MaterialDto> GetMaterialAsync(Guid id, CancellationToken ct = default);
    Task<MaterialDto> SaveMaterialAsync(Guid? id, SaveMaterialRequest req, CancellationToken ct = default);

    Task<PagedResult<PartyDto>> PartiesAsync(PartyKind kind, PageQuery page, bool? active, CancellationToken ct = default);
    Task<PartyDto> SavePartyAsync(PartyKind kind, Guid? id, SavePartyRequest req, CancellationToken ct = default);
}

public class MasterService(
    IAppDbContext db,
    IValidator<SaveMaterialRequest> materialValidator,
    IValidator<SavePartyRequest> partyValidator) : IMasterService
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

    public async Task<PagedResult<MaterialDto>> MaterialsAsync(PageQuery page, Guid? categoryId, bool? active, CancellationToken ct = default)
    {
        var q = db.Materials.AsNoTracking();
        if (categoryId is not null) q = q.Where(m => m.Subcategory.MaterialCategoryId == categoryId);
        if (active is not null) q = q.Where(m => m.IsActive == active);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(m => m.Name.Contains(page.Q) || m.Code.Contains(page.Q));
        return await q.OrderBy(m => m.Name).Select(MaterialProjection).ToPagedAsync(page, ct);
    }

    public async Task<MaterialDto> GetMaterialAsync(Guid id, CancellationToken ct = default)
        => await db.Materials.AsNoTracking().Where(m => m.Id == id).Select(MaterialProjection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Material", id);

    public async Task<MaterialDto> SaveMaterialAsync(Guid? id, SaveMaterialRequest req, CancellationToken ct = default)
    {
        await materialValidator.ValidateAndThrowAsync(req, ct);
        var code = req.Code.Trim();
        if (await db.Materials.AnyAsync(m => m.Code == code && m.Id != (id ?? Guid.Empty), ct))
            throw new AppException($"Material code '{code}' already exists.", 409);
        if (!await db.MaterialSubcategories.AnyAsync(s => s.Id == req.MaterialSubcategoryId, ct))
            throw new NotFoundException("MaterialSubcategory", req.MaterialSubcategoryId);
        if (!await db.Units.AnyAsync(u => u.Id == req.UnitId, ct))
            throw new NotFoundException("Unit", req.UnitId);

        var m = id is null ? new Material() : await db.Materials.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Material", id);

        m.Code = code; m.Name = req.Name.Trim(); m.MaterialSubcategoryId = req.MaterialSubcategoryId;
        m.UnitId = req.UnitId; m.SecondaryUnitId = req.SecondaryUnitId; m.ConversionFactor = req.ConversionFactor;
        m.MinStockLevel = req.MinStockLevel; m.ReorderLevel = req.ReorderLevel;
        m.DefaultPurchaseRate = req.DefaultPurchaseRate; m.GstRate = req.GstRate; m.IsActive = req.IsActive;
        m.Description = req.Description; m.Notes = req.Notes;

        if (id is null) db.Materials.Add(m);
        await db.SaveChangesAsync(ct);
        return await GetMaterialAsync(m.Id, ct);
    }

    public async Task<PagedResult<PartyDto>> PartiesAsync(PartyKind kind, PageQuery page, bool? active, CancellationToken ct = default)
    {
        var q = page.Q?.Trim();
        switch (kind)
        {
            case PartyKind.Contractor:
            {
                var src = db.Contractors.AsNoTracking();
                if (active is not null) src = src.Where(c => c.IsActive == active);
                if (!string.IsNullOrWhiteSpace(q)) src = src.Where(c => c.Name.Contains(q) || c.Code.Contains(q));
                return await src.OrderBy(c => c.Name)
                    .Select(c => new PartyDto(c.Id, c.Code, c.Name, c.CompanyName, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin, c.ContractorType, c.IsActive, c.Notes))
                    .ToPagedAsync(page, ct);
            }
            case PartyKind.Customer:
            {
                var src = db.Customers.AsNoTracking();
                if (active is not null) src = src.Where(c => c.IsActive == active);
                if (!string.IsNullOrWhiteSpace(q)) src = src.Where(c => c.Name.Contains(q) || c.Code.Contains(q));
                return await src.OrderBy(c => c.Name)
                    .Select(c => new PartyDto(c.Id, c.Code, c.Name, null, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin, null, c.IsActive, c.Notes))
                    .ToPagedAsync(page, ct);
            }
            default:
            {
                var src = db.Suppliers.AsNoTracking();
                if (active is not null) src = src.Where(c => c.IsActive == active);
                if (!string.IsNullOrWhiteSpace(q)) src = src.Where(c => c.Name.Contains(q) || c.Code.Contains(q));
                return await src.OrderBy(c => c.Name)
                    .Select(c => new PartyDto(c.Id, c.Code, c.Name, null, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin, null, c.IsActive, c.Notes))
                    .ToPagedAsync(page, ct);
            }
        }
    }

    public async Task<PartyDto> SavePartyAsync(PartyKind kind, Guid? id, SavePartyRequest req, CancellationToken ct = default)
    {
        await partyValidator.ValidateAndThrowAsync(req, ct);
        var code = req.Code.Trim();
        var other = id ?? Guid.Empty;
        Guid savedId;

        switch (kind)
        {
            case PartyKind.Contractor:
            {
                if (await db.Contractors.AnyAsync(c => c.Code == code && c.Id != other, ct))
                    throw new AppException($"Contractor code '{code}' already exists.", 409);
                var e = id is null ? new Contractor() : await db.Contractors.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Contractor", id);
                e.Code = code; e.Name = req.Name.Trim(); e.CompanyName = req.CompanyName; e.Mobile = req.Mobile;
                e.Email = req.Email; e.Address = req.Address; e.Pan = req.Pan; e.Gstin = req.Gstin;
                e.ContractorType = req.Type; e.IsActive = req.IsActive; e.Notes = req.Notes;
                if (id is null) db.Contractors.Add(e);
                savedId = e.Id; break;
            }
            case PartyKind.Customer:
            {
                if (await db.Customers.AnyAsync(c => c.Code == code && c.Id != other, ct))
                    throw new AppException($"Customer code '{code}' already exists.", 409);
                var e = id is null ? new Customer() : await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Customer", id);
                e.Code = code; e.Name = req.Name.Trim(); e.Mobile = req.Mobile; e.Email = req.Email;
                e.Address = req.Address; e.Pan = req.Pan; e.Gstin = req.Gstin; e.IsActive = req.IsActive; e.Notes = req.Notes;
                if (id is null) db.Customers.Add(e);
                savedId = e.Id; break;
            }
            default:
            {
                if (await db.Suppliers.AnyAsync(c => c.Code == code && c.Id != other, ct))
                    throw new AppException($"Supplier code '{code}' already exists.", 409);
                var e = id is null ? new Supplier() : await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Supplier", id);
                e.Code = code; e.Name = req.Name.Trim(); e.Mobile = req.Mobile; e.Email = req.Email;
                e.Address = req.Address; e.Pan = req.Pan; e.Gstin = req.Gstin; e.IsActive = req.IsActive; e.Notes = req.Notes;
                if (id is null) db.Suppliers.Add(e);
                savedId = e.Id; break;
            }
        }

        await db.SaveChangesAsync(ct);
        return (await PartiesAsync(kind, new PageQuery { PageSize = 200 }, null, ct)).Items.First(p => p.Id == savedId);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Material, MaterialDto>> MaterialProjection =
        m => new MaterialDto(m.Id, m.Code, m.Name, m.MaterialSubcategoryId, m.Subcategory.Name,
            m.Subcategory.Category.Name, m.UnitId, m.Unit.Code, m.MinStockLevel, m.ReorderLevel,
            m.DefaultPurchaseRate, m.GstRate, m.IsActive, m.Description, m.Notes);
}
