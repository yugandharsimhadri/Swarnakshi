using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiry { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
    public ICollection<UserSiteAssignment> SiteAssignments { get; set; } = new List<UserSiteAssignment>();
}

/// <summary>Fine-grained overrides (mainly for SubOwner). Key values live in Application.Security.Permissions.</summary>
public class UserPermission : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string PermissionKey { get; set; } = null!;
    public bool Granted { get; set; } = true;
}

/// <summary>Scopes a Supervisor (or others) to specific sites.</summary>
public class UserSiteAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
}
