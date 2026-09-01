using System.Security.Claims;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Services;

namespace Swarnakshi.Api.Common;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub"), out var id)
            ? id : null;

    /// <summary>Read only from the signed token — never from a header, route or body.</summary>
    public Guid? CompanyId =>
        Guid.TryParse(User?.FindFirstValue(SwarnakshiClaims.CompanyId), out var id) ? id : null;

    public bool IsPlatformAdmin =>
        User?.FindFirstValue(SwarnakshiClaims.TokenKind) == SwarnakshiClaims.PlatformKind;

    public string? Username => User?.FindFirstValue(SwarnakshiClaims.Username);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(User?.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(SwarnakshiClaims.Permission).Select(c => c.Value).ToArray() ?? [];

    /// <summary>A platform operator holds no company permissions — it is not a user of any company.</summary>
    public bool Has(string permissionKey) => !IsPlatformAdmin && Permissions.Contains(permissionKey);
}
