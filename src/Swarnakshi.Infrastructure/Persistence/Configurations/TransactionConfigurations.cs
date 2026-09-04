using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// Everything that carries a transaction number, and the line items that hang off them.

/// TxnNumberConfigs covers all of them together: the numbering rule and the tenant-unique index
/// are the same for every transactional document, and one statement of it cannot drift.
/// </summary>

public class TxnNumberConfigs :
    IEntityTypeConfiguration<PurchaseHeader>, IEntityTypeConfiguration<MaterialRequest>,
    IEntityTypeConfiguration<ProjectExpense>, IEntityTypeConfiguration<SiteExpense>, IEntityTypeConfiguration<LabourEntry>,
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

        // The hot path of the whole product. Every cost figure the app shows — the dashboard, the
        // company summary, each villa's total, profitability, budget burn — is a sum over posted
        // rows of one tenant, grouped by project or by type. Without this the server table-scans
        // ProjectExpenses for each of them: measured at 128x the seed data it scanned 649 times in
        // one short run, and SQL Server's own missing-index DMV put the improvement at 96-99%.
        //
        // The INCLUDE is what makes it a covering index: the aggregate is answered from the index
        // alone, with no lookup back into the table for the amount or the grouping key.
        //
        // ProjectId is in the key rather than the INCLUDE because the two shapes of question want
        // different things. "What has this villa cost?" seeks straight to one project; "what has
        // the company spent?" scans the index, which is a fraction of the table's width. Leading
        // with CompanyId is for the tenant filter, though within one tenant's database every row
        // shares it, so the selectivity that matters comes from Status and ProjectId.
        e.HasIndex(x => new { x.CompanyId, x.Status, x.ProjectId })
            .IncludeProperties(x => new { x.ExpenseType, x.Amount, x.Date })
            .HasDatabaseName("IX_ProjectExpenses_CompanyId_Status_Covering");
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Head).WithMany().HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Subhead).WithMany().HasForeignKey(x => x.ExpenseSubheadId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SiteExpense> e)
    {
        e.HasIndex(x => new { x.CompanyId, x.TxnNumber }).IsUnique();
        e.HasIndex(x => new { x.SiteId, x.Date });
        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Head).WithMany().HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
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
        e.HasOne(x => x.DeliverToProject).WithMany().HasForeignKey(x => x.DeliverToProjectId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ExpenseHead).WithMany().HasForeignKey(x => x.ExpenseHeadId).OnDelete(DeleteBehavior.Restrict);
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
