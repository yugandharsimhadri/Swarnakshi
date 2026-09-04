using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// Staff and what they have been paid.
/// </summary>

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
