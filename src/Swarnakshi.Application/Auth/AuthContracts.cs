using System.ComponentModel.DataAnnotations;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Auth;

/// <summary>
/// One box, two audiences: <c>username@companycode</c> for a company user, a bare username for a
/// platform operator. Keeping it one field means nobody has to know which kind of account they
/// hold before they can sign in.
/// </summary>
public record LoginRequest([Required] string Login, [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record CompanyDto(Guid Id, string Code, string Name, DateOnly LicenseExpiresOn, int DaysToExpiry, bool IsActive);

public record AuthUserDto(
    Guid Id,
    string Name,
    string Username,
    string Login,
    string? Email,
    UserRole Role,
    bool IsCompanyAdmin,
    IReadOnlyCollection<string> Permissions);

/// <summary>Sign-in result. <paramref name="Kind"/> tells the client which console to open.</summary>
public record AuthResponse(
    string Kind,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto? User,
    CompanyDto? Company,
    PlatformUserDto? PlatformUser)
{
    public const string TenantKind = "tenant";
    public const string PlatformKind = "platform";
}

public record PlatformUserDto(Guid Id, string Username, string DisplayName);

// ---- Company registration (public, unauthenticated) ---------------------
public record RegisterCompanyRequest(
    [Required] string CompanyName,
    [Required] string CompanyCode,
    [Required] string Username,
    [Required] string Password,
    [Required] string ConfirmPassword,
    string? ContactEmail,
    string? ContactMobile);

public record RegisterCompanyResponse(Guid CompanyId, string CompanyCode, string CompanyName, string Login, DateOnly LicenseExpiresOn);
