using Swarnakshi.Application.Platform;
using Swarnakshi.Infrastructure.Persistence.Seed;

namespace Swarnakshi.Infrastructure.Persistence;

/// <summary>
/// Gives a newly registered company the same master data the founding tenant has: units, the
/// 50-category material taxonomy with its specification fields, expense heads and subheads, labour
/// categories, payment methods, project types and default settings.
///
/// Every company owns its own copy rather than sharing a global catalogue — a builder must be able
/// to rename a category or retire a unit without changing anybody else's product.
/// </summary>
public sealed class CompanyProvisioner(AppDbContext db) : ICompanyProvisioner
{
    public async Task ProvisionAsync(Guid companyId, CancellationToken ct = default)
    {
        using var scope = db.BeginTenantScope(companyId);
        await MasterDataSeeder.RunAsync(db, ct);
    }
}
