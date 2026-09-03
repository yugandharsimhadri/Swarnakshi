using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Platform;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Users;

public record UserDto(Guid Id, string Name, string Username, string Login, string? Email, string? Mobile,
    UserRole Role, bool IsActive, bool IsCompanyAdmin,
    IReadOnlyList<string> ExtraPermissions, IReadOnlyList<Guid> SiteIds);

public record CreateUserRequest(string Name, string Username, string Password, UserRole Role, string? Email, string? Mobile = null);
public record UpdateUserRequest(string Name, UserRole Role, bool IsActive, string? Mobile = null);
public record SetPasswordRequest(string Password);
public record SetPermissionsRequest(List<string> Permissions);
public record SetSitesRequest(List<Guid> SiteIds);

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Username).Must(u => LoginIdentity.IsValidUsername(LoginIdentity.NormaliseUsername(u)))
            .WithMessage("Username must be 3-60 characters: lowercase letters, digits, dot, underscore or hyphen.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Mobile).Must(m => LoginIdentity.IsValidMobile(m))
            .When(x => !string.IsNullOrWhiteSpace(x.Mobile))
            .WithMessage("Enter a 10-digit mobile number.");
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("Password must be at least 8 characters.");
    }
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default);
    Task SetPasswordAsync(Guid id, SetPasswordRequest req, CancellationToken ct = default);
    Task<UserDto> SetPermissionsAsync(Guid id, SetPermissionsRequest req, CancellationToken ct = default);
    Task<UserDto> SetSitesAsync(Guid id, SetSitesRequest req, CancellationToken ct = default);
    IReadOnlyList<string> AllPermissionKeys();
}

public class UserService(
    IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, IDateTimeProvider clock,
    IValidator<CreateUserRequest> createValidator) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var code = await CompanyCodeAsync(ct);
        return (await db.Users.AsNoTracking()
                .Include(u => u.Permissions).Include(u => u.SiteAssignments)
                .OrderBy(u => u.Name).ToListAsync(ct))
            .Select(u => Map(u, code)).ToList();
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct = default)
        => Map(await Load(id, ct), await CompanyCodeAsync(ct));

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(req, ct);
        var username = LoginIdentity.NormaliseUsername(req.Username);

        // Unique within this company only — the tenant filter scopes the check, and the composite
        // index (CompanyId, Username) is what actually enforces it.
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            throw new AppException($"A user with username '{username}' already exists in this company.", 409);

        var mobile = LoginIdentity.NormaliseMobile(req.Mobile);
        if (mobile is not null && await db.Users.AnyAsync(u => u.Mobile == mobile, ct))
            throw new AppException($"Mobile number '{mobile}' is already used by another user in this company.", 409);

        var user = new User
        {
            Name = req.Name.Trim(),
            Username = username,
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant(),
            Mobile = mobile,
            PasswordHash = hasher.Hash(req.Password),
            Role = req.Role,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        var user = await Load(id, ct);
        if (user.Id == currentUser.UserId && (user.Role != req.Role || !req.IsActive))
            throw new AppException("You cannot change your own role or deactivate yourself.", 409);
        if (user.Role == UserRole.Owner && req.Role != UserRole.Owner
            && !await db.Users.AnyAsync(u => u.Role == UserRole.Owner && u.Id != id && u.IsActive, ct))
            throw new AppException("There must be at least one active Owner.", 409);

        var mobile = LoginIdentity.NormaliseMobile(req.Mobile);
        if (mobile is not null && await db.Users.AnyAsync(u => u.Mobile == mobile && u.Id != id, ct))
            throw new AppException($"Mobile number '{mobile}' is already used by another user in this company.", 409);

        user.Name = req.Name.Trim();
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        user.Mobile = mobile;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetPasswordAsync(Guid id, SetPasswordRequest req, CancellationToken ct = default)
    {
        if ((req.Password ?? "").Length < 8) throw new AppException("Password must be at least 8 characters.", 400);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id);
        user.PasswordHash = hasher.Hash(req.Password!);
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.TokensValidFrom = clock.Now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> SetPermissionsAsync(Guid id, SetPermissionsRequest req, CancellationToken ct = default)
    {
        var user = await Load(id, ct);
        var valid = req.Permissions.Where(Permissions.All.Contains).Distinct().ToList();

        db.UserPermissions.RemoveRange(user.Permissions);
        foreach (var key in valid)
            db.UserPermissions.Add(new UserPermission { UserId = user.Id, PermissionKey = key, Granted = true });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<UserDto> SetSitesAsync(Guid id, SetSitesRequest req, CancellationToken ct = default)
    {
        var user = await Load(id, ct);
        var siteIds = await db.Sites.Where(s => req.SiteIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);

        db.UserSiteAssignments.RemoveRange(user.SiteAssignments);
        foreach (var sid in siteIds)
            db.UserSiteAssignments.Add(new UserSiteAssignment { UserId = user.Id, SiteId = sid });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public IReadOnlyList<string> AllPermissionKeys() => Permissions.All;

    private async Task<User> Load(Guid id, CancellationToken ct)
        => await db.Users.Include(u => u.Permissions).Include(u => u.SiteAssignments)
               .FirstOrDefaultAsync(u => u.Id == id, ct)
           ?? throw new NotFoundException("User", id);

    /// <summary>The tenant's code, so each row can show the login exactly as the person types it.</summary>
    private async Task<string> CompanyCodeAsync(CancellationToken ct)
    {
        var companyId = currentUser.CompanyId ?? throw new AppException("No company in scope.", 401);
        return await db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId).Select(c => c.Code).FirstOrDefaultAsync(ct) ?? "?";
    }

    private static UserDto Map(User u, string companyCode) => new(
        u.Id, u.Name, u.Username, LoginIdentity.Format(u.Username, companyCode), u.Email, u.Mobile,
        u.Role, u.IsActive, u.IsCompanyAdmin,
        u.Permissions.Where(p => p.Granted).Select(p => p.PermissionKey).ToList(),
        u.SiteAssignments.Select(a => a.SiteId).ToList());
}
