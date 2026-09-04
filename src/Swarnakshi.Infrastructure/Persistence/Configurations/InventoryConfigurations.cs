using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// What each site holds and every movement in or out of it.
/// </summary>

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

        // The stock ledger as a person reads it: one site, newest first. The index above leads with
        // MaterialId, which answers "what happened to cement" and not "what happened at this site",
        // so that screen and the consumption register were scanning the table instead. Type is in
        // the key because the consumption register wants only the issues.
        e.HasIndex(x => new { x.CompanyId, x.SiteId, x.Type, x.Date })
            .HasDatabaseName("IX_InventoryTransactions_CompanyId_SiteId_Type_Date");

        e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
