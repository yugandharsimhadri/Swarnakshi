using Swarnakshi.Domain.Common;

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

public class Material : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid MaterialSubcategoryId { get; set; }
    public MaterialSubcategory Subcategory { get; set; } = null!;
    public string? Description { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public Guid? SecondaryUnitId { get; set; }
    public Unit? SecondaryUnit { get; set; }
    public decimal? ConversionFactor { get; set; }

    public decimal MinStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal DefaultPurchaseRate { get; set; }
    public decimal? GstRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
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
