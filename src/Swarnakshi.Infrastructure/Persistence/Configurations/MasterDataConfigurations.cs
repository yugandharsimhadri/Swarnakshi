using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// The catalogues: materials and their specifications, units, categories, expense heads, settings.

/// MasterCodeConfig deliberately configures several entities at once. They share one rule — a code
/// unique inside a company — and stating it once is the point; a file per entity would copy it.
/// </summary>

public class MaterialConfig : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(40);
        e.Property(x => x.Brand).HasMaxLength(120);
        e.Property(x => x.GenericMeasurement).HasMaxLength(120);
        e.Property(x => x.SpecSummary).HasMaxLength(400);
        e.Property(x => x.SpecSignature).HasMaxLength(500).IsRequired();

        // Server-side duplicate prevention: name + brand + identity specs.
        e.HasIndex(x => new { x.CompanyId, x.SpecSignature }).IsUnique();
        e.HasIndex(x => x.Brand);
        e.HasIndex(x => x.IsActive);

        e.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.MaterialSubcategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SecondaryUnit).WithMany().HasForeignKey(x => x.SecondaryUnitId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Specifications).WithOne(x => x.Material)
            .HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MaterialSpecDefinitionConfig : IEntityTypeConfiguration<MaterialSpecDefinition>
{
    public void Configure(EntityTypeBuilder<MaterialSpecDefinition> e)
    {
        e.Property(x => x.Key).HasMaxLength(60).IsRequired();
        e.Property(x => x.Label).HasMaxLength(120).IsRequired();
        e.Property(x => x.Options).HasMaxLength(600);
        e.HasIndex(x => new { x.CompanyId, x.MaterialSubcategoryId, x.Key }).IsUnique();
        e.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.MaterialSubcategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MaterialSpecValueConfig : IEntityTypeConfiguration<MaterialSpecValue>
{
    public void Configure(EntityTypeBuilder<MaterialSpecValue> e)
    {
        e.Property(x => x.Value).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.CompanyId, x.MaterialId, x.MaterialSpecDefinitionId }).IsUnique();
        e.HasIndex(x => x.Value);
        // Restrict: a definition still used by a material must not vanish underneath it.
        e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.MaterialSpecDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UnitConfig : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(20);
    }
}

public class MasterCodeConfig :
    IEntityTypeConfiguration<Contractor>, IEntityTypeConfiguration<Customer>, IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Contractor> e) => e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
    public void Configure(EntityTypeBuilder<Customer> e) => e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
    public void Configure(EntityTypeBuilder<Supplier> e) => e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
}

public class SubcategoryConfig : IEntityTypeConfiguration<MaterialSubcategory>
{
    public void Configure(EntityTypeBuilder<MaterialSubcategory> e)
    {
        e.HasOne(x => x.Category).WithMany(x => x.Subcategories)
            .HasForeignKey(x => x.MaterialCategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CompanyId, x.MaterialCategoryId, x.Name }).IsUnique();
    }
}

public class ExpenseSubheadConfig : IEntityTypeConfiguration<ExpenseSubhead>
{
    public void Configure(EntityTypeBuilder<ExpenseSubhead> e)
    {
        e.HasOne(x => x.Head).WithMany(x => x.Subheads)
            .HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CompanyId, x.ExpenseHeadId, x.Name }).IsUnique();
    }
}

public class SettingConfig : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Key, x.SiteId }).IsUnique();
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
