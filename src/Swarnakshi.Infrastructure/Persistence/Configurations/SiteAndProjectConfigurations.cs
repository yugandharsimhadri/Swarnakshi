using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// Where work happens and what is being built.
/// </summary>

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
