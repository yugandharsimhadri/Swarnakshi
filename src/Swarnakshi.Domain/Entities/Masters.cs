using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class Unit : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class MaterialCategory : BaseEntity
{
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<MaterialSubcategory> Subcategories { get; set; } = new List<MaterialSubcategory>();
}

public class MaterialSubcategory : BaseEntity
{
    public Guid MaterialCategoryId { get; set; }
    public MaterialCategory Category { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// An exact purchasable / stockable construction material. Identity is
/// Name + Brand + the identity-bearing specifications of its subcategory.
/// Never holds stock — current stock lives in <see cref="InventoryBalance"/> per site.
/// </summary>
public class Material : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid MaterialSubcategoryId { get; set; }
    public MaterialSubcategory Subcategory { get; set; } = null!;

    /// <summary>Company / brand, e.g. "Polycab", "Tata Steel", "MS Steel" (a brand name, not mild steel).</summary>
    public string? Brand { get; set; }
    public string? Description { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public Guid? SecondaryUnitId { get; set; }
    public Unit? SecondaryUnit { get; set; }
    public decimal? ConversionFactor { get; set; }

    /// <summary>Free-form package/physical note, e.g. "90 Meter / Coil". Not a unit conversion.</summary>
    public string? GenericMeasurement { get; set; }

    public decimal MinStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    /// <summary>Reference rate only — inventory is valued from actual landed purchase rates.</summary>
    public decimal DefaultPurchaseRate { get; set; }
    public decimal? GstRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<MaterialSpecValue> Specifications { get; set; } = new List<MaterialSpecValue>();

    /// <summary>Denormalised display string, e.g. "25 mm · Cold Water". Rebuilt on every save.</summary>
    public string? SpecSummary { get; set; }

    /// <summary>Normalised duplicate key over name + brand + identity specs. Unique index.</summary>
    public string SpecSignature { get; set; } = null!;
}

/// <summary>Declares one specification field for a subcategory — what the Add/Edit form renders.</summary>
public class MaterialSpecDefinition : BaseEntity
{
    public Guid MaterialSubcategoryId { get; set; }
    public MaterialSubcategory Subcategory { get; set; } = null!;

    /// <summary>Stable machine key, e.g. "diameter". Unique within the subcategory.</summary>
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public SpecFieldKind Kind { get; set; } = SpecFieldKind.Text;

    /// <summary>Pipe-separated choices when <see cref="Kind"/> is Select.</summary>
    public string? Options { get; set; }
    public bool IsRequired { get; set; }

    /// <summary>When true the value participates in the duplicate signature (material identity).</summary>
    public bool PartOfIdentity { get; set; } = true;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One specification value held by one material.</summary>
public class MaterialSpecValue : BaseEntity
{
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid MaterialSpecDefinitionId { get; set; }
    public MaterialSpecDefinition Definition { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class ExpenseHead : BaseEntity
{
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ExpenseSubhead> Subheads { get; set; } = new List<ExpenseSubhead>();
}

public class ExpenseSubhead : BaseEntity
{
    public Guid ExpenseHeadId { get; set; }
    public ExpenseHead Head { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class LabourCategory : BaseEntity
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class PaymentMethod : BaseEntity
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class ProjectType : BaseEntity
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class Supplier : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Pan { get; set; }
    public string? Gstin { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>Global — a contractor works across many sites/projects. Never duplicated per project.</summary>
public class Contractor : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? CompanyName { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Pan { get; set; }
    public string? Gstin { get; set; }
    public string? BankDetails { get; set; }
    public string? ContractorType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>Global. Optionally linked to one or more projects.</summary>
public class Customer : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Pan { get; set; }
    public string? Gstin { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>Key/value config. SiteId null = global default; set = per-site override.</summary>
public class Setting : BaseEntity
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
}
