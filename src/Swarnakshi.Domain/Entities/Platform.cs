using Swarnakshi.Domain.Common;

namespace Swarnakshi.Domain.Entities;

/// <summary>
/// A tenant. Swarnakshi is one company with many sites; another builder signing up is another
/// company with its own sites, masters, users and numbering — sharing nothing but the deployment.
/// </summary>
public class Company : PlatformEntity
{
    /// <summary>Login namespace and tenant key. Globally unique, lowercase, no '@'. Immutable once issued.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Display name. Deliberately NOT unique — two builders may legitimately share a name.</summary>
    public string Name { get; set; } = null!;

    public string? ContactEmail { get; set; }
    public string? ContactMobile { get; set; }

    /// <summary>Access ends at the START of this day (UTC). Only an EnterpriseAdmin can move it.</summary>
    public DateOnly LicenseExpiresOn { get; set; }

    /// <summary>Set false to suspend a tenant without deleting anything.</summary>
    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public bool IsLicenseValidOn(DateOnly today) => today <= LicenseExpiresOn;
}

/// <summary>
/// An operator of the platform itself — not a user of any company. A platform user can reset a
/// company admin's password and move a licence expiry, and can do nothing else: it has no
/// CompanyId, so every tenant query filter excludes it by construction.
/// </summary>
public class PlatformUser : PlatformEntity
{
    public string Username { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiry { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
