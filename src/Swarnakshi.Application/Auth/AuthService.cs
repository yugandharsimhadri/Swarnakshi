using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Platform;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<AuthResponse> MeAsync(CancellationToken ct = default);
}

public class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService tokens,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IAuthService
{
    /// <summary>Deliberately identical for a bad username, a bad company and a bad password.</summary>
    private const string BadCredentials = "Invalid username or password.";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (!LoginIdentity.TryParse(request.Login, out var id))
            throw new AppException(BadCredentials, 401);

        return id.IsPlatform
            ? await PlatformLoginAsync(id.Username, request.Password, ct)
            : await TenantLoginAsync(id.Username, id.CompanyCode!, request.Password, ct);
    }

    private async Task<AuthResponse> TenantLoginAsync(string username, string companyCode, string password, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == companyCode, ct);
        if (company is null) throw new AppException(BadCredentials, 401);

        // Reading a user needs the tenant in scope — the global filter is on, and at this point
        // nobody is signed in, so there is no ambient company to filter by.
        using var scope = db.BeginTenantScope(company.Id);

        var user = await db.Users.Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        if (user is null || !user.IsActive || !hasher.Verify(password, user.PasswordHash))
            throw new AppException(BadCredentials, 401);

        if (!company.IsActive)
            throw new AppException("This company account is suspended. Contact your administrator.", 403);

        // Reported at sign-in rather than on the first API call: being told at the door beats
        // signing in successfully and then finding every screen refuses to load.
        if (!company.IsLicenseValidOn(clock.Today))
            throw new AppException(
                $"The licence for {company.Name} expired on {company.LicenseExpiresOn:dd MMM yyyy}. " +
                "Ask your Swarnakshi administrator to renew it.", 402);

        return await IssueTenantAsync(user, company, ct);
    }

    private async Task<AuthResponse> PlatformLoginAsync(string username, string password, CancellationToken ct)
    {
        var op = await db.PlatformUsers.FirstOrDefaultAsync(p => p.Username == username, ct);
        if (op is null || !op.IsActive || !hasher.Verify(password, op.PasswordHash))
            throw new AppException(BadCredentials, 401);

        var pair = tokens.IssuePlatform(op);
        op.RefreshToken = pair.RefreshToken;
        op.RefreshTokenExpiry = pair.RefreshTokenExpiresAt;
        op.LastLoginAt = clock.Now;
        await db.SaveChangesAsync(ct);

        return new AuthResponse(AuthResponse.PlatformKind, pair.AccessToken, pair.AccessTokenExpiresAt,
            pair.RefreshToken, pair.RefreshTokenExpiresAt, null, null,
            new PlatformUserDto(op.Id, op.Username, op.DisplayName));
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var token = request.RefreshToken;

        var op = await db.PlatformUsers.FirstOrDefaultAsync(p => p.RefreshToken == token, ct);
        if (op is not null)
        {
            if (!op.IsActive || op.RefreshTokenExpiry is null || op.RefreshTokenExpiry < clock.Now)
                throw new AppException("Invalid or expired refresh token.", 401);

            var platformPair = tokens.IssuePlatform(op);
            op.RefreshToken = platformPair.RefreshToken;
            op.RefreshTokenExpiry = platformPair.RefreshTokenExpiresAt;
            await db.SaveChangesAsync(ct);
            return new AuthResponse(AuthResponse.PlatformKind, platformPair.AccessToken, platformPair.AccessTokenExpiresAt,
                platformPair.RefreshToken, platformPair.RefreshTokenExpiresAt, null, null,
                new PlatformUserDto(op.Id, op.Username, op.DisplayName));
        }

        // The refresh token itself identifies the tenant, so this one read crosses the filter.
        var match = await db.Users.IgnoreQueryFilters()
            .Where(u => u.RefreshToken == token)
            .Select(u => new { u.Id, u.CompanyId })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Invalid or expired refresh token.", 401);

        using var scope = db.BeginTenantScope(match.CompanyId);

        var user = await db.Users.Include(u => u.Permissions).FirstAsync(u => u.Id == match.Id, ct);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == match.CompanyId, ct);

        if (!user.IsActive || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < clock.Now)
            throw new AppException("Invalid or expired refresh token.", 401);
        if (!company.IsActive || !company.IsLicenseValidOn(clock.Today))
            throw new AppException("This company's licence is no longer valid.", 402);

        return await IssueTenantAsync(user, company, ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } id) return;

        if (currentUser.IsPlatformAdmin)
        {
            var op = await db.PlatformUsers.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (op is null) return;
            op.RefreshToken = null;
            op.RefreshTokenExpiry = null;
        }
        else
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return;
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> MeAsync(CancellationToken ct = default)
    {
        var id = currentUser.UserId ?? throw new AppException("Not signed in.", 401);

        if (currentUser.IsPlatformAdmin)
        {
            var op = await db.PlatformUsers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new NotFoundException("PlatformUser", id);
            return new AuthResponse(AuthResponse.PlatformKind, "", default, "", default, null, null,
                new PlatformUserDto(op.Id, op.Username, op.DisplayName));
        }

        var user = await db.Users.AsNoTracking().Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);
        var company = await db.Companies.AsNoTracking().FirstAsync(c => c.Id == user.CompanyId, ct);

        return new AuthResponse(AuthResponse.TenantKind, "", default, "", default,
            ToDto(user, company), ToDto(company, clock.Today), null);
    }

    private async Task<AuthResponse> IssueTenantAsync(User user, Company company, CancellationToken ct)
    {
        var perms = ResolvePermissions(user);
        var pair = tokens.Issue(user, company, perms);
        user.RefreshToken = pair.RefreshToken;
        user.RefreshTokenExpiry = pair.RefreshTokenExpiresAt;
        await db.SaveChangesAsync(ct);

        return new AuthResponse(AuthResponse.TenantKind, pair.AccessToken, pair.AccessTokenExpiresAt,
            pair.RefreshToken, pair.RefreshTokenExpiresAt,
            ToDto(user, company, perms), ToDto(company, clock.Today), null);
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

    private static AuthUserDto ToDto(User user, Company company, IReadOnlyCollection<string>? perms = null)
        => new(user.Id, user.Name, user.Username, LoginIdentity.Format(user.Username, company.Code),
            user.Email, user.Role, user.IsCompanyAdmin, perms ?? ResolvePermissions(user));

    internal static CompanyDto ToDto(Company c, DateOnly today)
        => new(c.Id, c.Code, c.Name, c.LicenseExpiresOn,
            c.LicenseExpiresOn.DayNumber - today.DayNumber, c.IsActive);
}
