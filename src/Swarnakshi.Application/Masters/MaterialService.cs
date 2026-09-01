using System.Globalization;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Masters;

// ---- DTOs ----------------------------------------------------------------

/// <summary>One specification field a subcategory declares — drives the dynamic form.</summary>
public record SpecDefinitionDto(Guid Id, Guid MaterialSubcategoryId, string Key, string Label,
    SpecFieldKind Kind, IReadOnlyList<string> Options, bool IsRequired, bool PartOfIdentity, int SortOrder);

public record MaterialSpecDto(Guid DefinitionId, string Key, string Label, string Value, int SortOrder);

/// <summary>Row shape for the Material Master list/table.</summary>
public record MaterialListDto(Guid Id, string Code, string Name, string? Brand,
    Guid MaterialSubcategoryId, string SubcategoryName, Guid MaterialCategoryId, string CategoryName,
    string? SpecSummary, Guid UnitId, string UnitCode, decimal DefaultPurchaseRate, decimal? GstRate,
    bool IsActive);

/// <summary>Full material record for the detail / edit view.</summary>
public record MaterialDetailDto(Guid Id, string Code, string Name, string? Brand,
    Guid MaterialSubcategoryId, string SubcategoryName, Guid MaterialCategoryId, string CategoryName,
    string? SpecSummary, IReadOnlyList<MaterialSpecDto> Specifications,
    Guid UnitId, string UnitCode, Guid? SecondaryUnitId, string? SecondaryUnitCode, decimal? ConversionFactor,
    string? GenericMeasurement, decimal DefaultPurchaseRate, decimal? GstRate,
    decimal MinStockLevel, decimal ReorderLevel, string? Description, string? Notes,
    bool IsActive, bool CodeLocked, bool HasStock, decimal TotalStock);

/// <summary>Site-level stock for the detail view — read from inventory, never stored on Material.</summary>
public record MaterialSiteStockDto(Guid SiteId, string SiteName, decimal Quantity, decimal AverageRate, decimal Value);

public record MaterialSummaryDto(int Total, int Active, int Inactive, int Categories);

public record SaveMaterialRequest(string Code, string Name, Guid MaterialSubcategoryId, string? Brand,
    Guid UnitId, Guid? SecondaryUnitId, decimal? ConversionFactor, string? GenericMeasurement,
    decimal MinStockLevel, decimal ReorderLevel, decimal DefaultPurchaseRate, decimal? GstRate,
    string? Description, string? Notes, IReadOnlyDictionary<string, string?>? Specifications);

public class SaveMaterialValidator : AbstractValidator<SaveMaterialRequest>
{
    public SaveMaterialValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialSubcategoryId).NotEmpty();
        RuleFor(x => x.UnitId).NotEmpty();
        RuleFor(x => x.Brand).MaximumLength(120);
        RuleFor(x => x.GenericMeasurement).MaximumLength(120);
        RuleFor(x => x.DefaultPurchaseRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStockLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GstRate).InclusiveBetween(0, 100).When(x => x.GstRate is not null);
        RuleFor(x => x.ConversionFactor).GreaterThan(0)
            .When(x => x.ConversionFactor is not null)
            .WithMessage("Conversion factor must be greater than zero.");
        RuleFor(x => x.ConversionFactor).NotNull()
            .When(x => x.SecondaryUnitId is not null)
            .WithMessage("Conversion factor is required when a secondary unit is set.");
        RuleFor(x => x.SecondaryUnitId).NotEqual(x => x.UnitId)
            .When(x => x.SecondaryUnitId is not null)
            .WithMessage("Secondary unit must differ from the primary unit.");
    }
}

// ---- Service -------------------------------------------------------------

public interface IMaterialService
{
    Task<PagedResult<MaterialListDto>> ListAsync(PageQuery page, Guid? categoryId, Guid? subcategoryId,
        string? brand, Guid? unitId, bool? active, CancellationToken ct = default);
    Task<MaterialSummaryDto> SummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> BrandsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SpecDefinitionDto>> SpecDefinitionsAsync(Guid? subcategoryId, CancellationToken ct = default);

    Task<MaterialDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MaterialSiteStockDto>> SiteStockAsync(Guid id, CancellationToken ct = default);

    Task<MaterialDetailDto> CreateAsync(SaveMaterialRequest req, CancellationToken ct = default);
    Task<MaterialDetailDto> UpdateAsync(Guid id, SaveMaterialRequest req, CancellationToken ct = default);
    Task<MaterialDetailDto> DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<MaterialDetailDto> ReactivateAsync(Guid id, CancellationToken ct = default);
}

public class MaterialService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<SaveMaterialRequest> validator) : IMaterialService
{
    // ---- queries ---------------------------------------------------------

    public async Task<PagedResult<MaterialListDto>> ListAsync(PageQuery page, Guid? categoryId,
        Guid? subcategoryId, string? brand, Guid? unitId, bool? active, CancellationToken ct = default)
    {
        var q = db.Materials.AsNoTracking();

        if (categoryId is not null) q = q.Where(m => m.Subcategory.MaterialCategoryId == categoryId);
        if (subcategoryId is not null) q = q.Where(m => m.MaterialSubcategoryId == subcategoryId);
        if (unitId is not null) q = q.Where(m => m.UnitId == unitId);
        if (active is not null) q = q.Where(m => m.IsActive == active);
        if (!string.IsNullOrWhiteSpace(brand)) q = q.Where(m => m.Brand == brand);

        // Lowercase both sides: EF translates string.Contains to SQLite's instr(), which is
        // case-sensitive, so "cement" would miss "OPC 53 Grade Cement". ToLower() maps to
        // lower()/LOWER() on both providers, keeping this SQLite-agnostic.
        var term = page.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(m =>
                m.Code.ToLower().Contains(term) ||
                m.Name.ToLower().Contains(term) ||
                (m.Brand != null && m.Brand.ToLower().Contains(term)) ||
                m.Subcategory.Name.ToLower().Contains(term) ||
                m.Subcategory.Category.Name.ToLower().Contains(term) ||
                (m.SpecSummary != null && m.SpecSummary.ToLower().Contains(term)) ||
                m.Specifications.Any(v => v.Value.ToLower().Contains(term)));

        q = page.Sort switch
        {
            "code" => q.OrderBy(m => m.Code),
            "-code" => q.OrderByDescending(m => m.Code),
            "-name" => q.OrderByDescending(m => m.Name),
            "rate" => q.OrderBy(m => m.DefaultPurchaseRate),
            "-rate" => q.OrderByDescending(m => m.DefaultPurchaseRate),
            _ => q.OrderBy(m => m.Name).ThenBy(m => m.Code)
        };

        return await q.Select(m => new MaterialListDto(m.Id, m.Code, m.Name, m.Brand,
                m.MaterialSubcategoryId, m.Subcategory.Name, m.Subcategory.MaterialCategoryId,
                m.Subcategory.Category.Name, m.SpecSummary, m.UnitId, m.Unit.Code,
                m.DefaultPurchaseRate, m.GstRate, m.IsActive))
            .ToPagedAsync(page, ct);
    }

    public async Task<MaterialSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var total = await db.Materials.CountAsync(ct);
        var active = await db.Materials.CountAsync(m => m.IsActive, ct);
        var cats = await db.MaterialCategories.CountAsync(c => c.IsActive, ct);
        return new MaterialSummaryDto(total, active, total - active, cats);
    }

    public async Task<IReadOnlyList<string>> BrandsAsync(CancellationToken ct = default)
        => await db.Materials.AsNoTracking()
            .Where(m => m.Brand != null && m.Brand != "")
            .Select(m => m.Brand!).Distinct().OrderBy(b => b).ToListAsync(ct);

    public async Task<IReadOnlyList<SpecDefinitionDto>> SpecDefinitionsAsync(Guid? subcategoryId, CancellationToken ct = default)
    {
        var q = db.MaterialSpecDefinitions.AsNoTracking().Where(d => d.IsActive);
        if (subcategoryId is not null) q = q.Where(d => d.MaterialSubcategoryId == subcategoryId);
        var rows = await q.OrderBy(d => d.MaterialSubcategoryId).ThenBy(d => d.SortOrder).ToListAsync(ct);
        return rows.Select(d => new SpecDefinitionDto(d.Id, d.MaterialSubcategoryId, d.Key, d.Label,
            d.Kind, SplitOptions(d.Options), d.IsRequired, d.PartOfIdentity, d.SortOrder)).ToList();
    }

    public async Task<MaterialDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Materials.AsNoTracking()
            .Include(x => x.Subcategory).ThenInclude(s => s.Category)
            .Include(x => x.Unit)
            .Include(x => x.SecondaryUnit)
            .Include(x => x.Specifications).ThenInclude(v => v.Definition)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Material", id);

        var locked = await IsReferencedAsync(id, ct);
        var stock = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.MaterialId == id).SumAsync(b => (decimal?)b.Quantity, ct) ?? 0m;

        return Map(m, locked, stock);
    }

    public async Task<IReadOnlyList<MaterialSiteStockDto>> SiteStockAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Materials.AnyAsync(m => m.Id == id, ct)) throw new NotFoundException("Material", id);
        return await db.InventoryBalances.AsNoTracking()
            .Where(b => b.MaterialId == id)
            .OrderBy(b => b.Site.Name)
            .Select(b => new MaterialSiteStockDto(b.SiteId, b.Site.Name, b.Quantity, b.AverageRate, b.Value))
            .ToListAsync(ct);
    }

    // ---- writes ----------------------------------------------------------

    public Task<MaterialDetailDto> CreateAsync(SaveMaterialRequest req, CancellationToken ct = default)
        => SaveAsync(null, req, ct);

    public Task<MaterialDetailDto> UpdateAsync(Guid id, SaveMaterialRequest req, CancellationToken ct = default)
        => SaveAsync(id, req, ct);

    private async Task<MaterialDetailDto> SaveAsync(Guid? id, SaveMaterialRequest req, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(req, ct);

        var code = req.Code.Trim();
        var name = req.Name.Trim();
        var brand = Blank(req.Brand);

        var sub = await db.MaterialSubcategories.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == req.MaterialSubcategoryId, ct)
            ?? throw new NotFoundException("MaterialSubcategory", req.MaterialSubcategoryId);

        if (!await db.Units.AnyAsync(u => u.Id == req.UnitId, ct))
            throw new NotFoundException("Unit", req.UnitId);
        if (req.SecondaryUnitId is not null && !await db.Units.AnyAsync(u => u.Id == req.SecondaryUnitId, ct))
            throw new NotFoundException("Unit", req.SecondaryUnitId);

        Material material;
        if (id is null)
        {
            material = new Material();
            db.Materials.Add(material);
        }
        else
        {
            material = await db.Materials
                .Include(x => x.Specifications)
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Material", id);

            // Code is immutable once any transaction references the material, so history stays unambiguous.
            if (!string.Equals(material.Code, code, StringComparison.Ordinal)
                && await IsReferencedAsync(material.Id, ct))
                throw new AppException(
                    "This material has transaction history, so its code can no longer be changed.", 409);
        }

        if (await db.Materials.AnyAsync(m => m.Code == code && m.Id != material.Id, ct))
            throw new AppException($"Material code '{code}' already exists.", 409);

        // ---- specifications -------------------------------------------------
        var defs = await db.MaterialSpecDefinitions.AsNoTracking()
            .Where(d => d.MaterialSubcategoryId == sub.Id && d.IsActive)
            .OrderBy(d => d.SortOrder).ToListAsync(ct);

        var supplied = req.Specifications ?? new Dictionary<string, string?>();
        var unknown = supplied.Keys.Where(k => !string.IsNullOrWhiteSpace(supplied[k]))
            .Where(k => defs.All(d => !string.Equals(d.Key, k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unknown.Count > 0)
            throw new AppException(
                $"Specification(s) not applicable to this subcategory: {string.Join(", ", unknown)}.", 400);

        var resolved = new List<(MaterialSpecDefinition Def, string Value)>();
        foreach (var d in defs)
        {
            var raw = supplied.FirstOrDefault(kv =>
                string.Equals(kv.Key, d.Key, StringComparison.OrdinalIgnoreCase)).Value;
            var value = Blank(raw);

            if (value is null)
            {
                if (d.IsRequired) throw new AppException($"{d.Label} is required for this subcategory.", 400);
                continue;
            }

            if (d.Kind == SpecFieldKind.Number
                && !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                throw new AppException($"{d.Label} must be a number.", 400);

            var options = SplitOptions(d.Options);
            if (d.Kind == SpecFieldKind.Select && options.Count > 0
                && !options.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))
                throw new AppException($"{d.Label} must be one of: {string.Join(", ", options)}.", 400);

            resolved.Add((d, value));
        }

        var parts = resolved.Select(r => new SpecPart(r.Def.Key, r.Def.Label, r.Def.SortOrder,
            r.Def.PartOfIdentity, r.Value)).ToList();
        var signature = MaterialIdentity.Signature(name, brand, parts);
        if (await db.Materials.AnyAsync(m => m.SpecSignature == signature && m.Id != material.Id, ct))
            throw new AppException(
                "A material with the same name, company/brand and specifications already exists.", 409);

        var isNew = id is null;
        material.Code = code;
        material.Name = name;
        material.Brand = brand;
        material.MaterialSubcategoryId = sub.Id;
        material.UnitId = req.UnitId;
        material.SecondaryUnitId = req.SecondaryUnitId;
        material.ConversionFactor = req.SecondaryUnitId is null ? null : req.ConversionFactor;
        material.GenericMeasurement = Blank(req.GenericMeasurement);
        material.MinStockLevel = req.MinStockLevel;
        material.ReorderLevel = req.ReorderLevel;
        material.DefaultPurchaseRate = req.DefaultPurchaseRate;
        material.GstRate = req.GstRate;
        material.Description = Blank(req.Description);
        material.Notes = Blank(req.Notes);
        material.SpecSummary = MaterialIdentity.Summary(parts);
        material.SpecSignature = signature;

        SyncSpecValues(material, resolved, isNew);

        Audit(material, isNew ? "Material created" : "Material updated");
        await db.SaveChangesAsync(ct);
        return await GetAsync(material.Id, ct);
    }

    public async Task<MaterialDetailDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Material", id);

        if (!m.IsActive) return await GetAsync(id, ct);

        // Enforced here, not only in the UI: stock must be consumed or adjusted out first.
        var sites = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.MaterialId == id && b.Quantity > 0).CountAsync(ct);
        if (sites > 0)
            throw new AppException(
                "This material currently has stock at one or more sites. Consume or adjust the remaining " +
                "stock before deactivating the material.", 409);

        m.IsActive = false;
        Audit(m, "Material deactivated");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<MaterialDetailDto> ReactivateAsync(Guid id, CancellationToken ct = default)
    {
        var m = await db.Materials.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Material", id);

        if (m.IsActive) return await GetAsync(id, ct);
        m.IsActive = true;
        Audit(m, "Material reactivated");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ---- helpers ---------------------------------------------------------

    /// <summary>True when any transaction anywhere points at this material.</summary>
    private async Task<bool> IsReferencedAsync(Guid materialId, CancellationToken ct)
        => await db.PurchaseItems.AnyAsync(x => x.MaterialId == materialId, ct)
        || await db.MaterialRequestItems.AnyAsync(x => x.MaterialId == materialId, ct)
        || await db.InventoryTransactions.AnyAsync(x => x.MaterialId == materialId, ct)
        || await db.InventoryBalances.AnyAsync(x => x.MaterialId == materialId, ct);

    private void SyncSpecValues(Material material, List<(MaterialSpecDefinition Def, string Value)> resolved, bool isNew)
    {
        foreach (var existing in material.Specifications.ToList())
        {
            var match = resolved.FirstOrDefault(r => r.Def.Id == existing.MaterialSpecDefinitionId);
            if (match.Def is null) db.MaterialSpecValues.Remove(existing);
            else existing.Value = match.Value;
        }

        foreach (var (def, value) in resolved)
            if (material.Specifications.All(v => v.MaterialSpecDefinitionId != def.Id))
            {
                var row = new MaterialSpecValue
                {
                    MaterialId = material.Id,
                    MaterialSpecDefinitionId = def.Id,
                    Value = value
                };
                material.Specifications.Add(row);
                // Explicit Add: BaseEntity pre-populates Id, so a child reached only through a
                // tracked parent is classified Modified and EF emits an UPDATE for a missing row.
                if (!isNew) db.MaterialSpecValues.Add(row);
            }
    }

    /// <summary>Material is a plain master, so audit rows are written explicitly rather than by the
    /// AuditableEntity hook in <c>SaveChangesAsync</c>.</summary>
    private void Audit(Material m, string action) => db.AuditLogs.Add(new AuditLog
    {
        EntityType = nameof(Material),
        EntityId = m.Id,
        Action = action,
        DataJson = $"{m.Code} · {m.Name}",
        UserId = currentUser.UserId,
        At = DateTimeOffset.UtcNow
    });

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IReadOnlyList<string> SplitOptions(string? options)
        => string.IsNullOrWhiteSpace(options)
            ? Array.Empty<string>()
            : options.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static MaterialDetailDto Map(Material m, bool codeLocked, decimal totalStock) => new(
        m.Id, m.Code, m.Name, m.Brand,
        m.MaterialSubcategoryId, m.Subcategory.Name, m.Subcategory.MaterialCategoryId, m.Subcategory.Category.Name,
        m.SpecSummary,
        m.Specifications.OrderBy(v => v.Definition.SortOrder)
            .Select(v => new MaterialSpecDto(v.MaterialSpecDefinitionId, v.Definition.Key,
                v.Definition.Label, v.Value, v.Definition.SortOrder)).ToList(),
        m.UnitId, m.Unit.Code, m.SecondaryUnitId, m.SecondaryUnit?.Code, m.ConversionFactor,
        m.GenericMeasurement, m.DefaultPurchaseRate, m.GstRate, m.MinStockLevel, m.ReorderLevel,
        m.Description, m.Notes, m.IsActive, codeLocked, totalStock > 0, totalStock);
}
