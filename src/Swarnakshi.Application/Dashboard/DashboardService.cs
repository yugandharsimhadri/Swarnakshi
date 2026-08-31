using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Dashboard;

public record KpiCard(string Label, decimal Value, string Format); // Format: "money" | "count"
public record RecentTxn(string Type, string Ref, DateOnly Date, decimal Amount, string? Context);
public record DashboardPayload(string Role, IReadOnlyList<KpiCard> Kpis, IReadOnlyList<RecentTxn> Recent, int PendingApprovals);

public interface IDashboardService
{
    Task<DashboardPayload> GetAsync(CancellationToken ct = default);
}

public class DashboardService(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IDashboardService
{
    public async Task<DashboardPayload> GetAsync(CancellationToken ct = default)
    {
        var role = currentUser.Role ?? UserRole.Supervisor;
        var monthStart = new DateOnly(clock.Today.Year, clock.Today.Month, 1);

        var pending = currentUser.Has(Security.Permissions.ApprovalsDecide)
            ? await db.ApprovalRequests.CountAsync(a => a.CurrentStatus == TransactionStatus.PendingApproval, ct)
            : 0;

        var kpis = new List<KpiCard>();
        var recent = new List<RecentTxn>();

        var inventoryValue = await db.InventoryBalances.SumAsync(b => (decimal?)b.Value, ct) ?? 0m;
        var projectCost = await db.ProjectExpenses.Where(e => e.Status == TransactionStatus.Posted)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var monthPurchases = await db.PurchaseHeaders
            .Where(p => p.Status == TransactionStatus.Posted && p.Date >= monthStart)
            .SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0m;
        var monthExpenses = await db.ProjectExpenses
            .Where(e => e.Status == TransactionStatus.Posted && e.Date >= monthStart)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var saleValue = await db.Projects.SumAsync(p => (decimal?)(p.ContractSaleValue ?? 0m), ct) ?? 0m;
        var received = await db.CustomerPayments.Where(p => p.Status == TransactionStatus.Posted)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var contractorPayable = await db.ContractWorks.SumAsync(w => (decimal?)w.Balance, ct) ?? 0m;

        var lowStock = await db.InventoryBalances
            .CountAsync(b => b.Material.MinStockLevel > 0 && b.Quantity <= b.Material.MinStockLevel, ct);

        if (role is UserRole.Owner or UserRole.SubOwner)
        {
            kpis.Add(new KpiCard("Projects", await db.Projects.CountAsync(ct), "count"));
            kpis.Add(new KpiCard("Active sites", await db.Sites.CountAsync(s => s.Status == SiteStatus.Active, ct), "count"));
            kpis.Add(new KpiCard("Inventory value", inventoryValue, "money"));
            kpis.Add(new KpiCard("Project cost (all)", projectCost, "money"));
            kpis.Add(new KpiCard("Purchases this month", monthPurchases, "money"));
            kpis.Add(new KpiCard("Expenses this month", monthExpenses, "money"));
            kpis.Add(new KpiCard("Customer receivable", saleValue - received, "money"));
            kpis.Add(new KpiCard("Contractor payable", contractorPayable, "money"));
            kpis.Add(new KpiCard("Low stock items", lowStock, "count"));
        }
        else if (role is UserRole.Accountant)
        {
            var draftContractor = await db.ContractorPayments.CountAsync(p => p.Status == TransactionStatus.Draft, ct);
            var draftLabour = await db.LabourEntries.CountAsync(l => l.Status == TransactionStatus.Draft, ct);
            kpis.Add(new KpiCard("Contractor payable", contractorPayable, "money"));
            kpis.Add(new KpiCard("Customer receivable", saleValue - received, "money"));
            kpis.Add(new KpiCard("Receipts this month",
                await db.CustomerPayments.Where(p => p.Status == TransactionStatus.Posted && p.Date >= monthStart)
                    .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m, "money"));
            kpis.Add(new KpiCard("Draft contractor payments", draftContractor, "count"));
            kpis.Add(new KpiCard("Draft labour entries", draftLabour, "count"));
            kpis.Add(new KpiCard("Expenses this month", monthExpenses, "money"));
        }
        else // Supervisor
        {
            var mySites = await db.UserSiteAssignments.Where(a => a.UserId == currentUser.UserId)
                .Select(a => a.SiteId).ToListAsync(ct);
            var siteFilter = mySites.Count > 0;
            kpis.Add(new KpiCard("My sites", siteFilter ? mySites.Count : await db.Sites.CountAsync(ct), "count"));
            kpis.Add(new KpiCard("Projects",
                await db.Projects.CountAsync(p => !siteFilter || mySites.Contains(p.SiteId), ct), "count"));
            kpis.Add(new KpiCard("My pending requests",
                await db.MaterialRequests.CountAsync(r => r.RequestStatus == MaterialRequestStatus.PendingApproval
                    && (!siteFilter || mySites.Contains(r.SiteId)), ct), "count"));
            kpis.Add(new KpiCard("Approved, not issued",
                await db.MaterialRequests.CountAsync(r => r.RequestStatus == MaterialRequestStatus.Approved
                    && (!siteFilter || mySites.Contains(r.SiteId)), ct), "count"));
            kpis.Add(new KpiCard("Low stock items", lowStock, "count"));
        }

        var recentPurchases = await db.PurchaseHeaders.AsNoTracking()
            .Where(p => p.Status == TransactionStatus.Posted)
            .OrderByDescending(p => p.Date).Take(5)
            .Select(p => new RecentTxn("Purchase", p.TxnNumber, p.Date, p.TotalAmount, p.Supplier.Name))
            .ToListAsync(ct);
        var recentExpenses = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.Status == TransactionStatus.Posted)
            .OrderByDescending(e => e.Date).Take(5)
            .Select(e => new RecentTxn(e.ExpenseType.ToString(), e.TxnNumber, e.Date, e.Amount, e.Project.Name))
            .ToListAsync(ct);
        recent.AddRange(recentPurchases.Concat(recentExpenses).OrderByDescending(r => r.Date).Take(8));

        return new DashboardPayload(role.ToString(), kpis, recent, pending);
    }
}
