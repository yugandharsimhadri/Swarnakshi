using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

public class CompanyConfig : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> e)
    {
        // The one globally unique identifier in the system: it is the login namespace, so two
        // companies sharing a code would make "owner@acme" ambiguous. Names may repeat freely.
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.Code).HasMaxLength(30);
        e.Property(x => x.Name).HasMaxLength(200);
        e.HasIndex(x => x.Name);
    }
}

public class PlatformUserConfig : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> e)
    {
        e.HasIndex(x => x.Username).IsUnique();
        e.Property(x => x.Username).HasMaxLength(60);
        e.Property(x => x.DisplayName).HasMaxLength(200);
    }
}

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        // Unique per company, not globally: two builders may each have an "owner".
        e.HasIndex(x => new { x.CompanyId, x.Username }).IsUnique();
        e.Property(x => x.Username).HasMaxLength(60);
        e.Property(x => x.Email).HasMaxLength(256);
        e.Property(x => x.Name).HasMaxLength(200);
        e.HasMany(x => x.Permissions).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.SiteAssignments).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SiteConfig : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(30);
        e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.SetNull);
        e.HasMany(x => x.Projects).WithOne(x => x.Site).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProjectConfig : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(30);
        e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ProjectType).WithMany().HasForeignKey(x => x.ProjectTypeId).OnDelete(DeleteBehavior.SetNull);
    }
}

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

public class InventoryBalanceConfig : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.SiteId, x.MaterialId }).IsUnique();
        e.HasOne(x => x.Site).WithMany(x => x.InventoryBalances).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryTransactionConfig : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasIndex(x => new { x.SiteId, x.MaterialId, x.Date });
        e.HasIndex(x => x.ProjectId);
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TxnNumberConfigs :
    IEntityTypeConfiguration<PurchaseHeader>, IEntityTypeConfiguration<MaterialRequest>,
    IEntityTypeConfiguration<ProjectExpense>, IEntityTypeConfiguration<LabourEntry>,
    IEntityTypeConfiguration<ContractorPayment>, IEntityTypeConfiguration<CustomerPayment>
{
    public void Configure(EntityTypeBuilder<PurchaseHeader> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasMany(x => x.Items).WithOne(x => x.Header).HasForeignKey(x => x.PurchaseHeaderId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Payments).WithOne(x => x.Header).HasForeignKey(x => x.PurchaseHeaderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.MaterialRequest).WithMany().HasForeignKey(x => x.MaterialRequestId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<MaterialRequest> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasMany(x => x.Items).WithOne(x => x.Request).HasForeignKey(x => x.MaterialRequestId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ProjectExpense> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasIndex(x => new { x.ProjectId, x.Date });
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Head).WithMany().HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Subhead).WithMany().HasForeignKey(x => x.ExpenseSubheadId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<LabourEntry> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.LabourCategory).WithMany().HasForeignKey(x => x.LabourCategoryId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ContractorPayment> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ContractWork).WithMany(x => x.Payments).HasForeignKey(x => x.ContractWorkId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<CustomerPayment> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaterialRequestItemConfig : IEntityTypeConfiguration<MaterialRequestItem>
{
    public void Configure(EntityTypeBuilder<MaterialRequestItem> e)
    {
        e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ExpenseHead).WithMany().HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ExpenseSubhead).WithMany().HasForeignKey(x => x.ExpenseSubheadId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseItemConfig : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> e)
    {
        e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ContractWorkConfig : IEntityTypeConfiguration<ContractWork>
{
    public void Configure(EntityTypeBuilder<ContractWork> e)
    {
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalConfig : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> e)
    {
        e.HasIndex(x => new { x.EntityType, x.EntityId });
        e.HasIndex(x => x.CurrentStatus);
        e.HasMany(x => x.History).WithOne(x => x.Request).HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SequenceConfig : IEntityTypeConfiguration<TransactionSequence>
{
    public void Configure(EntityTypeBuilder<TransactionSequence> e)
        => e.HasIndex(x => new { x.CompanyId, x.Prefix, x.Year }).IsUnique();
}

public class AttachmentConfig : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> e)
        => e.HasIndex(x => new { x.EntityType, x.EntityId });
}

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        e.Property(x => x.Code).HasMaxLength(40);
        e.Property(x => x.Name).HasMaxLength(200);
        e.Property(x => x.Phone).HasMaxLength(20);
        e.Property(x => x.Designation).HasMaxLength(120);
        // Phone is how site staff are actually found, so it is worth an index of its own.
        e.HasIndex(x => new { x.CompanyId, x.Phone });
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
        e.HasMany(x => x.Payments).WithOne(x => x.Employee).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeePaymentConfig : IEntityTypeConfiguration<EmployeePayment>
{
    public void Configure(EntityTypeBuilder<EmployeePayment> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasIndex(x => new { x.EmployeeId, x.Date });
        e.Ignore(x => x.NetPaid);   // derived; kept out of the schema so it cannot drift
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
