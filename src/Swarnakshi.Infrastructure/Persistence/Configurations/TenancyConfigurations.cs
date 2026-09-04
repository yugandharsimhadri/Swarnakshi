using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// Who the row belongs to and who may sign in: the company, the platform operator, and users.
/// </summary>

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
        e.Property(x => x.Mobile).HasMaxLength(20);
        // Not a unique index: a filtered unique index is provider-specific, and SQL Server treats
        // NULLs as equal in a plain one. Uniqueness within a company is enforced in UserService;
        // login-by-mobile tolerates a cross-company clash by asking for the username instead.
        e.HasIndex(x => x.Mobile);
        e.Property(x => x.Name).HasMaxLength(200);
        e.HasMany(x => x.Permissions).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.SiteAssignments).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
