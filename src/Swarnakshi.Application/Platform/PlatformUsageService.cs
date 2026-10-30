using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Platform;

/// <summary>
/// How a company is getting on with the product, in one word.
///
/// <para>The distinction that matters is between a company that has not started yet and one that
/// has stopped. They look identical in a list of licences and need opposite phone calls.</para>
/// </summary>
public enum Engagement
{
    /// <summary>Registered in the last fortnight and not going yet. Too early to worry.</summary>
    JustRegistered = 0,
    /// <summary>Past a fortnight old and has never entered anything. The one to ring.</summary>
    NeverStarted = 1,
    /// <summary>Has real data, but nothing has been entered for a month.</summary>
    Dormant = 2,
    /// <summary>Entering things week to week.</summary>
    Active = 3
}

public record CompanyUsageDto(
    Guid Id, string Code, string Name, string? ContactEmail, string? ContactMobile,
    DateTimeOffset RegisteredAt, int DaysSinceRegistered,
    DateOnly LicenseExpiresOn, int DaysToExpiry, bool IsExpired, bool IsActive,
    int Users, int Sites, int Projects, int Records,
    DateTimeOffset? LastActivityAt, int? DaysSinceActivity, int Writes7, int Writes30,
    Engagement Engagement);

public record PlatformUsageDto(
    int Companies, int Active, int Dormant, int NeverStarted, int JustRegistered,
    int ExpiringSoon, int Expired,
    int TotalProjects, int TotalRecords, int Writes30,
    IReadOnlyList<CompanyUsageDto> Items);

public interface IPlatformUsageService
{
    Task<PlatformUsageDto> UsageAsync(CancellationToken ct = default);
}

/// <summary>
/// What the operator needs to know that a list of licences cannot tell them: which customers are
/// actually using the thing they registered for.
///
/// <para>Counts and timestamps only. This service reads how many rows a company has and when it
/// last wrote one — never what is in them. No name of a villa, no supplier, no amount, nothing
/// from <c>AuditLog.DataJson</c>. That line is the whole reason an EnterpriseAdmin token carries
/// no CompanyId: the tenant filters shut every business table, and this is a deliberate hole cut
/// to exactly the size of a number.</para>
///
/// <para>Activity comes from the audit trail, which stamps every write with its company and the
/// moment it happened. It began on 9 September 2026, so a company quiet since before then shows no
/// last-activity date — which, for the question being asked, is the right answer anyway.</para>
/// </summary>
public class PlatformUsageService(IAppDbContext db, IDateTimeProvider clock) : IPlatformUsageService
{
    public async Task<PlatformUsageDto> UsageAsync(CancellationToken ct = default)
    {
        var now = clock.Now;
        var today = clock.Today;
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-30);

        var companies = await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var ids = companies.Select(c => c.Id).ToList();
        if (ids.Count == 0)
            return new PlatformUsageDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, []);

        // One query per fact across every company, never one per company: this screen always shows
        // all of them, so a per-row lookup is a textbook N+1 on the one page guaranteed to hit it.
        async Task<Dictionary<Guid, int>> Tally(IQueryable<Guid> companyIds) =>
            await companyIds.GroupBy(id => id)
                .Select(g => new { Id = g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

        var users = await Tally(db.Users.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId));
        var sites = await Tally(db.Sites.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId));
        var projects = await Tally(db.Projects.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId));

        // "Records entered" is the work a company has actually put in: the documents its people
        // type, not the master data a new tenant is seeded with. Seeded units, materials and
        // expense heads are deliberately absent — every company has them on day one and counting
        // them would make an empty tenant look busy.
        var records = new Dictionary<Guid, int>();
        foreach (var set in new[]
                 {
                     db.PurchaseHeaders.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.MaterialRequests.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.ProjectExpenses.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.SiteExpenses.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.LabourEntries.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.ContractorPayments.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.CustomerPayments.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.EmployeePayments.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.InventoryTransactions.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                     db.AddOnRequests.IgnoreQueryFilters().Where(x => ids.Contains(x.CompanyId)).Select(x => x.CompanyId),
                 })
        {
            foreach (var (id, n) in await Tally(set))
                records[id] = records.GetValueOrDefault(id) + n;
        }

        // Activity means the company's own people working, and nothing else. Registering a tenant
        // seeds its units, materials and expense heads, and that seeding writes audit rows like any
        // other change — so counted naively, every customer looks busy for their first month
        // whether or not a soul has opened the app, which is the exact illusion this screen exists
        // to dispel. The rows that count are the ones written by a user belonging to that company:
        // seeding carries either no user at all or whoever was running it, and neither is the
        // customer doing their job.
        var companyUsers = db.Users.IgnoreQueryFilters();
        var activity = (await db.AuditLogs.IgnoreQueryFilters()
                .Where(a => ids.Contains(a.CompanyId)
                            && companyUsers.Any(u => u.Id == a.UserId && u.CompanyId == a.CompanyId))
                .GroupBy(a => a.CompanyId)
                .Select(g => new
                {
                    CompanyId = g.Key,
                    Last = g.Max(x => x.At),
                    W7 = g.Count(x => x.At >= since7),
                    W30 = g.Count(x => x.At >= since30)
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.CompanyId);

        var items = companies.Select(c =>
        {
            var act = activity.GetValueOrDefault(c.Id);
            var recs = records.GetValueOrDefault(c.Id);
            var proj = projects.GetValueOrDefault(c.Id);
            var age = (int)(now - c.CreatedAt).TotalDays;
            var lastAt = act?.Last;
            var w30 = act?.W30 ?? 0;

            var engagement =
                w30 > 0 ? Engagement.Active
                : recs > 0 || proj > 0 ? Engagement.Dormant
                : age <= 14 ? Engagement.JustRegistered
                : Engagement.NeverStarted;

            return new CompanyUsageDto(
                c.Id, c.Code, c.Name, c.ContactEmail, c.ContactMobile,
                c.CreatedAt, age,
                c.LicenseExpiresOn, c.LicenseExpiresOn.DayNumber - today.DayNumber,
                c.LicenseExpiresOn < today, c.IsActive,
                users.GetValueOrDefault(c.Id), sites.GetValueOrDefault(c.Id), proj, recs,
                lastAt, lastAt is null ? null : (int)(now - lastAt.Value).TotalDays,
                act?.W7 ?? 0, w30,
                engagement);
        }).ToList();

        // Worst first: the operator opens this to find the customers who need a call, not to admire
        // the ones who are fine. Within a level, the quietest for longest comes first.
        var ordered = items
            .OrderBy(i => (int)i.Engagement)
            .ThenByDescending(i => i.DaysSinceActivity ?? int.MaxValue)
            .ThenBy(i => i.Name)
            .ToList();

        return new PlatformUsageDto(
            items.Count,
            items.Count(i => i.Engagement == Engagement.Active),
            items.Count(i => i.Engagement == Engagement.Dormant),
            items.Count(i => i.Engagement == Engagement.NeverStarted),
            items.Count(i => i.Engagement == Engagement.JustRegistered),
            items.Count(i => !i.IsExpired && i.DaysToExpiry <= 30),
            items.Count(i => i.IsExpired),
            items.Sum(i => i.Projects),
            items.Sum(i => i.Records),
            items.Sum(i => i.Writes30),
            ordered);
    }
}
