using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
        var companyId = await SeedDefaultCompanyAsync(db, hasher, options, today, ct);
        await BackfillAdoptedLoginsAsync(db, companyId, ct);
        return companyId;
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
    /// <summary>
    /// Gives a login to users adopted from a database that predates multi-tenancy.
    ///
    /// Two things combine to lock such a database out completely. The migration adds
    /// <c>Users.Username</c> with an empty default and never backfills it; and the founding-company
    /// seed above creates its owner only when there are NO users, which on an upgraded database is
    /// never. The company ends up with users who exist, are active, and cannot sign in — the login
    /// resolves a username within the company, and "" matches nobody.
    ///
    /// This runs on every startup rather than only when the company is first created, so a database
    /// already left in that state is healed by a restart instead of needing a manual UPDATE. It only
    /// ever fills a login that is missing; an existing one is never rewritten.
    /// </summary>
    private static async Task BackfillAdoptedLoginsAsync(AppDbContext db, Guid companyId, CancellationToken ct)
    {
        using var scope = db.BeginTenantScope(companyId);

        // Ordered so the choice of admin below, and any numeric suffix, is the same on every run.
        var users = await db.Users.OrderBy(u => u.Name).ThenBy(u => u.Id).ToListAsync(ct);
        if (users.Count == 0) return;

        var taken = users
            .Where(u => !NeedsLogin(u))
            .Select(u => u.Username)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var user in users.Where(NeedsLogin))
        {
            user.Username = Unique(Derive(user.Email, user.Name), taken);
            taken.Add(user.Username);
        }

        // IsCompanyAdmin was added defaulting to false, so an adopted company has nobody who can
        // administer it — the owner would sign in only to find the company's own settings closed.
        if (!users.Any(u => u.IsCompanyAdmin))
        {
            var admin = users.FirstOrDefault(u => u is { Role: UserRole.Owner, IsActive: true })
                     ?? users.FirstOrDefault(u => u.IsActive);
            if (admin is not null) admin.IsCompanyAdmin = true;
        }

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// True when this user cannot sign in and needs a login derived for them: either the migration's
    /// empty default, or the row-id placeholder it writes so the unique index can be built. Neither
    /// is something a person could type.
    /// </summary>
    private static bool NeedsLogin(User user)
        => string.IsNullOrWhiteSpace(user.Username)
        || string.Equals(user.Username, user.Id.ToString(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A username for someone who never had one: the local part of their email, falling back to
    /// their name. Before multi-tenancy the email WAS the login, so its local part is the login the
    /// person already knows — "owner@swarnakshi.local" becomes "owner", and they sign in as
    /// "owner@swarnakshi".
    /// </summary>
    private static string Derive(string? email, string? name)
    {
        var at = email?.IndexOf('@') ?? -1;
        var candidate = Clean(at > 0 ? email![..at] : email);

        if (candidate.Length < LoginIdentity.MinUsernameLength) candidate = Clean(name);
        if (candidate.Length < LoginIdentity.MinUsernameLength) candidate = "user";

        // Leave room for a numeric suffix rather than truncating one off the end later.
        const int room = LoginIdentity.MaxUsernameLength - 4;
        return candidate.Length > room ? candidate[..room] : candidate;
    }

    /// <summary>Reduces text to what <see cref="LoginIdentity.IsValidUsername"/> accepts.</summary>
    private static string Clean(string? value)
    {
        var chars = (value ?? string.Empty).Trim().ToLowerInvariant()
            .Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-');
        // The pattern requires the first character to be alphanumeric.
        return new string(chars.ToArray()).TrimStart('.', '_', '-');
    }

    private static string Unique(string candidate, IReadOnlySet<string> taken)
    {
        if (!taken.Contains(candidate)) return candidate;
        for (var n = 2; ; n++)
        {
            var next = $"{candidate}{n}";
            if (!taken.Contains(next)) return next;
        }
    }

    private static async Task AdoptOrphanedRowsAsync(AppDbContext db, Guid companyId, CancellationToken ct)
    {
        var empty = Guid.Empty;
        // Quote through the provider rather than by hand: SQL Server writes [Sites], SQLite "Sites",
        // and the schema-qualified form differs again. The helper is the provider's own.
        var sql = db.GetService<ISqlGenerationHelper>();

        foreach (var (schema, table) in TenantTables(db))
        {
            var name = sql.DelimitIdentifier(table, schema);
            var column = sql.DelimitIdentifier(nameof(Domain.Common.ITenantOwned.CompanyId));
            await db.Database.ExecuteSqlRawAsync(
                $"UPDATE {name} SET {column} = {{0}} WHERE {column} = {{1}}",
                [companyId, empty], ct);
        }
    }

    private static IEnumerable<(string? Schema, string Table)> TenantTables(AppDbContext db) =>
        db.Model.GetEntityTypes()
            .Where(t => typeof(Domain.Common.ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => (t.GetSchema(), Table: t.GetTableName()))
            .Where(x => !string.IsNullOrEmpty(x.Table))
            .Distinct()
            .Select(x => (x.Item1, x.Table!));
}
