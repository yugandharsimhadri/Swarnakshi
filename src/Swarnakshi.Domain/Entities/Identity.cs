using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = null!;

    /// <summary>
    /// Local part of the login. Unique within the company, so the full login a person types is
    /// <c>username@companycode</c> — globally unique because company codes are.
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>Contact address. Optional, and NOT a login.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// A second way in. Stored as the canonical 10-digit form (see <c>LoginIdentity.NormaliseMobile</c>),
    /// so someone can sign in with just their phone number and skip the <c>@companycode</c>. Unique
    /// within a company; if the same number is used in two companies, login-by-mobile is ambiguous and
    /// the person is asked to use their username instead.
    /// </summary>
    public string? Mobile { get; set; }

    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The company admin created at registration. Only an EnterpriseAdmin can reset its password.</summary>
    public bool IsCompanyAdmin { get; set; }

    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiry { get; set; }

    /// <summary>
    /// Access tokens issued before this instant are refused. Clearing the refresh token alone would
    /// leave a live access token working for the rest of its hour — so a password reset, which
    /// exists precisely to take an account back, moves this too.
    /// </summary>
    public DateTimeOffset? TokensValidFrom { get; set; }

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
