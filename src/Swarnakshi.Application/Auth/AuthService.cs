using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
    Task<AuthUserDto> MeAsync(Guid userId, CancellationToken ct = default);
}

public class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService tokens,
    IDateTimeProvider clock) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            throw new AppException("Invalid email or password.", 401);

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, ct);

        if (user is null || !user.IsActive || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < clock.Now)
            throw new AppException("Invalid or expired refresh token.", 401);

        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthUserDto> MeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);
        return ToDto(user);
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var perms = ResolvePermissions(user);
        var pair = tokens.Issue(user, perms);
        user.RefreshToken = pair.RefreshToken;
        user.RefreshTokenExpiry = pair.RefreshTokenExpiresAt;
        await db.SaveChangesAsync(ct);
        return new AuthResponse(pair.AccessToken, pair.AccessTokenExpiresAt, pair.RefreshToken,
            pair.RefreshTokenExpiresAt, ToDto(user, perms));
    }

    private static IReadOnlyCollection<string> ResolvePermissions(User user)
    {
        var set = new HashSet<string>(Permissions.ForRole(user.Role));
        foreach (var p in user.Permissions)
        {
            if (p.Granted) set.Add(p.PermissionKey);
            else set.Remove(p.PermissionKey);
        }
        return set;
    }

    private static AuthUserDto ToDto(User user, IReadOnlyCollection<string>? perms = null)
        => new(user.Id, user.Name, user.Email, user.Role, perms ?? ResolvePermissions(user));
}
