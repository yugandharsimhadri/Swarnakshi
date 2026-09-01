using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Platform;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>Options for the platform operator and the first tenant, all overridable from configuration.</summary>
public sealed class PlatformSeedOptions
{
    public string EnterpriseAdminUsername { get; set; } = "EnterpriseAdmin";
    public string EnterpriseAdminPassword { get; set; } = "SivAyAAn@HMS";
    public string EnterpriseAdminDisplayName { get; set; } = "Enterprise Administrator";

    /// <summary>The founding tenant. Existing single-tenant data is adopted into it on upgrade.</summary>
    public string DefaultCompanyCode { get; set; } = "swarnakshi";
    public string DefaultCompanyName { get; set; } = "Swarnakshi";
    public string DefaultAdminUsername { get; set; } = "owner";
    public string DefaultAdminPassword { get; set; } = "Owner@123";
    public int DefaultCompanyLicenseDays { get; set; } = 3650;
}

/// <summary>
/// Seeds the platform operator and the founding company. Idempotent — safe on every startup.
///
/// Passwords here are creation-time defaults only: once a row exists this never touches it, so a
/// changed password is never quietly reset back by a restart.
/// </summary>
public static class PlatformSeeder
{
    public static async Task<Guid> RunAsync(
        AppDbContext db, IPasswordHasher hasher, PlatformSeedOptions options, DateOnly today, CancellationToken ct = default)
    {
        await SeedEnterpriseAdminAsync(db, hasher, options, ct);
        return await SeedDefaultCompanyAsync(db, hasher, options, today, ct);
    }

    private static async Task SeedEnterpriseAdminAsync(
        AppDbContext db, IPasswordHasher hasher, PlatformSeedOptions options, CancellationToken ct)
    {
        var username = LoginIdentity.NormaliseUsername(options.EnterpriseAdminUsername);
        if (await db.PlatformUsers.AnyAsync(p => p.Username == username, ct)) return;

        db.PlatformUsers.Add(new PlatformUser
        {
            Username = username,
            DisplayName = options.EnterpriseAdminDisplayName,
            PasswordHash = hasher.Hash(options.EnterpriseAdminPassword),
            IsActive = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guid> SeedDefaultCompanyAsync(
        AppDbContext db, IPasswordHasher hasher, PlatformSeedOptions options, DateOnly today, CancellationToken ct)
    {
        var code = LoginIdentity.NormaliseCode(options.DefaultCompanyCode);
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Code == code, ct);
        if (existing is not null) return existing.Id;

        var company = new Company
        {
            Code = code,
            Name = options.DefaultCompanyName,
            LicenseExpiresOn = today.AddDays(options.DefaultCompanyLicenseDays),
            IsActive = true,
            Notes = "Founding tenant, created by the platform seeder."
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        // Adopt any rows written before multi-tenancy existed. On a fresh database this matches
        // nothing; on an upgraded one it hands the whole existing business to the founding company
        // instead of stranding it under an empty tenant id.
        await AdoptOrphanedRowsAsync(db, company.Id, ct);

        using var scope = db.BeginTenantScope(company.Id);
        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.Add(new User
            {
                Name = options.DefaultCompanyName,
                Username = LoginIdentity.NormaliseUsername(options.DefaultAdminUsername),
                PasswordHash = hasher.Hash(options.DefaultAdminPassword),
                Role = UserRole.Owner,
                IsCompanyAdmin = true,
                IsActive = true
            });
            await db.SaveChangesAsync(ct);
        }

        return company.Id;
    }

    /// <summary>
    /// Raw SQL on purpose: this rewrites the tenant column itself, and every LINQ read is already
    /// filtered by that column — so the rows needing adoption are exactly the ones EF cannot see.
    /// </summary>
    private static async Task AdoptOrphanedRowsAsync(AppDbContext db, Guid companyId, CancellationToken ct)
    {
        var empty = Guid.Empty;
        foreach (var table in TenantTables(db))
        {
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE \"{table}\" SET \"CompanyId\" = {{0}} WHERE \"CompanyId\" = {{1}}",
                [companyId, empty], ct);
        }
    }

    private static IEnumerable<string> TenantTables(AppDbContext db) =>
        db.Model.GetEntityTypes()
            .Where(t => typeof(Domain.Common.ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.GetTableName())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()!
            .Cast<string>();
}
