using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Platform;

public record CompanyAdminDto(Guid UserId, string Name, string Username, string Login, string? Email, bool IsActive);

public record CompanyOverviewDto(
    Guid Id, string Code, string Name, string? ContactEmail, string? ContactMobile,
    DateOnly LicenseExpiresOn, int DaysToExpiry, bool IsExpired, bool IsActive,
    DateTimeOffset CreatedAt, int UserCount, int SiteCount, int ProjectCount,
    IReadOnlyList<CompanyAdminDto> Admins);

public record SetLicenseExpiryRequest(DateOnly ExpiresOn, string? Notes);
public record ExtendLicenseRequest(int Days);
public record ResetCompanyPasswordRequest(Guid UserId, string NewPassword, string ConfirmPassword);
public record ResetCompanyPasswordResponse(string Login, string CompanyName);
public record SetCompanyActiveRequest(bool IsActive);

/// <summary>
/// The EnterpriseAdmin console. Two jobs only — move a licence expiry, and reset a company admin's
/// password — and deliberately nothing else: no sites, projects, stock or money. A platform token
/// carries no CompanyId, so the tenant query filters exclude every business table from it anyway;
/// this service is the small, explicit hole in that wall, and it never reads business data.
/// </summary>
public interface IPlatformAdminService
{
    Task<IReadOnlyList<CompanyOverviewDto>> ListCompaniesAsync(string? search, CancellationToken ct = default);
    Task<CompanyOverviewDto> GetCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyOverviewDto> SetLicenseExpiryAsync(Guid companyId, SetLicenseExpiryRequest request, CancellationToken ct = default);
    Task<CompanyOverviewDto> ExtendLicenseAsync(Guid companyId, ExtendLicenseRequest request, CancellationToken ct = default);
    Task<CompanyOverviewDto> SetActiveAsync(Guid companyId, SetCompanyActiveRequest request, CancellationToken ct = default);
    Task<ResetCompanyPasswordResponse> ResetAdminPasswordAsync(Guid companyId, ResetCompanyPasswordRequest request, CancellationToken ct = default);
    Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, string confirmPassword, CancellationToken ct = default);
}

public class PlatformAdminService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IPlatformAdminService
{
    public async Task<IReadOnlyList<CompanyOverviewDto>> ListCompaniesAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Companies.AsNoTracking();
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(c => c.Name.Contains(term) || c.Code.Contains(term));

        var companies = await q.OrderBy(c => c.Name).ThenBy(c => c.Code).ToListAsync(ct);
        var ids = companies.Select(c => c.Id).ToList();

        // One query per fact across all companies rather than per company: the console lists every
        // tenant, so a per-row lookup would be a textbook N+1 on the one screen that always shows all.
        var counts = await db.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.CompanyId))
            .GroupBy(u => u.CompanyId)
            .Select(g => new { CompanyId = g.Key, Users = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Users, ct);

        var sites = await db.Sites.IgnoreQueryFilters()
            .Where(s => ids.Contains(s.CompanyId))
            .GroupBy(s => s.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        var projects = await db.Projects.IgnoreQueryFilters()
            .Where(p => ids.Contains(p.CompanyId))
            .GroupBy(p => p.CompanyId)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        var admins = await AdminsAsync(ids, ct);

        return companies.Select(c => Map(c,
            counts.GetValueOrDefault(c.Id), sites.GetValueOrDefault(c.Id), projects.GetValueOrDefault(c.Id),
            admins.GetValueOrDefault(c.Id, []))).ToList();
    }

    public async Task<CompanyOverviewDto> GetCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await LoadAsync(companyId, ct);
        var users = await db.Users.IgnoreQueryFilters().CountAsync(u => u.CompanyId == companyId, ct);
        var sites = await db.Sites.IgnoreQueryFilters().CountAsync(s => s.CompanyId == companyId, ct);
        var projects = await db.Projects.IgnoreQueryFilters().CountAsync(p => p.CompanyId == companyId, ct);
        var admins = await AdminsAsync([companyId], ct);
        return Map(company, users, sites, projects, admins.GetValueOrDefault(companyId, []));
    }

    public async Task<CompanyOverviewDto> SetLicenseExpiryAsync(Guid companyId, SetLicenseExpiryRequest request, CancellationToken ct = default)
    {
        var company = await LoadAsync(companyId, ct);
        company.LicenseExpiresOn = request.ExpiresOn;
        if (!string.IsNullOrWhiteSpace(request.Notes)) company.Notes = request.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return await GetCompanyAsync(companyId, ct);
    }

    public async Task<CompanyOverviewDto> ExtendLicenseAsync(Guid companyId, ExtendLicenseRequest request, CancellationToken ct = default)
    {
        if (request.Days is < 1 or > 3650)
            throw new AppException("Extend by between 1 and 3650 days.", 400);

        var company = await LoadAsync(companyId, ct);
        // Extend from today when the licence has already lapsed, so a renewal always buys the full
        // period rather than silently spending part of it on the days the tenant was locked out.
        var from = company.LicenseExpiresOn < clock.Today ? clock.Today : company.LicenseExpiresOn;
        company.LicenseExpiresOn = from.AddDays(request.Days);
        await db.SaveChangesAsync(ct);
        return await GetCompanyAsync(companyId, ct);
    }

    public async Task<CompanyOverviewDto> SetActiveAsync(Guid companyId, SetCompanyActiveRequest request, CancellationToken ct = default)
    {
        var company = await LoadAsync(companyId, ct);
        company.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return await GetCompanyAsync(companyId, ct);
    }

    public async Task<ResetCompanyPasswordResponse> ResetAdminPasswordAsync(Guid companyId, ResetCompanyPasswordRequest request, CancellationToken ct = default)
    {
        if ((request.NewPassword ?? "").Length < 8)
            throw new AppException("Password must be at least 8 characters.", 400);
        if (request.NewPassword != request.ConfirmPassword)
            throw new AppException("The two passwords do not match.", 400);

        var company = await LoadAsync(companyId, ct);

        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.CompanyId == companyId, ct)
            ?? throw new NotFoundException("User", request.UserId);

        user.PasswordHash = hasher.Hash(request.NewPassword!);
        // Cut every live session: a password reset that leaves old tokens working has not
        // actually taken the account back.
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        user.TokensValidFrom = clock.Now;
        await db.SaveChangesAsync(ct);

        return new ResetCompanyPasswordResponse(LoginIdentity.Format(user.Username, company.Code), company.Name);
    }

    public async Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, string confirmPassword, CancellationToken ct = default)
    {
        var id = currentUser.UserId ?? throw new AppException("Not signed in.", 401);
        if ((newPassword ?? "").Length < 8) throw new AppException("Password must be at least 8 characters.", 400);
        if (newPassword != confirmPassword) throw new AppException("The two passwords do not match.", 400);

        var op = await db.PlatformUsers.FirstOrDefaultAsync(p => p.Id == id, ct)
                 ?? throw new NotFoundException("PlatformUser", id);
        if (!hasher.Verify(currentPassword, op.PasswordHash))
            throw new AppException("Current password is incorrect.", 400);

        op.PasswordHash = hasher.Hash(newPassword!);
        op.RefreshToken = null;
        op.RefreshTokenExpiry = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Company> LoadAsync(Guid companyId, CancellationToken ct)
        => await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, ct)
           ?? throw new NotFoundException("Company", companyId);

    private async Task<Dictionary<Guid, List<CompanyAdminDto>>> AdminsAsync(List<Guid> companyIds, CancellationToken ct)
    {
        var codes = await db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);

        var rows = await db.Users.IgnoreQueryFilters()
            .Where(u => companyIds.Contains(u.CompanyId) && (u.IsCompanyAdmin || u.Role == Domain.Enums.UserRole.Owner))
            .OrderByDescending(u => u.IsCompanyAdmin).ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.CompanyId, u.Name, u.Username, u.Email, u.IsActive })
            .ToListAsync(ct);

        return rows
            .GroupBy(u => u.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(u => new CompanyAdminDto(
                u.Id, u.Name, u.Username,
                LoginIdentity.Format(u.Username, codes.GetValueOrDefault(u.CompanyId, "?")),
                u.Email, u.IsActive)).ToList());
    }

    private CompanyOverviewDto Map(Company c, int users, int sites, int projects, IReadOnlyList<CompanyAdminDto> admins)
        => new(c.Id, c.Code, c.Name, c.ContactEmail, c.ContactMobile, c.LicenseExpiresOn,
            c.LicenseExpiresOn.DayNumber - clock.Today.DayNumber, !c.IsLicenseValidOn(clock.Today),
            c.IsActive, c.CreatedAt, users, sites, projects, admins);
}
