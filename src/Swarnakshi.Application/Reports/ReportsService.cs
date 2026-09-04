using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Reports;

/// <param name="Note">
/// Set when the table is not the whole answer — currently only when a row-level report hit its
/// cap. A report that quietly returns the first N rows and looks complete is worse than one that
/// is slow, so anything that trims says so here and the UI shows it.
/// </param>
public record ReportTable(string Title, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows, string? Note = null);

public interface IReportsService
{
    Task<ReportTable> InventoryStockAsync(Guid? siteId, CancellationToken ct = default);
    Task<ReportTable> PurchaseRegisterAsync(DateOnly? from, DateOnly? to, Guid? siteId, CancellationToken ct = default);
    Task<ReportTable> ConsumptionRegisterAsync(DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct = default);
    Task<ReportTable> LowStockAsync(CancellationToken ct = default);
    Task<ReportTable> ProjectCostSummaryAsync(CancellationToken ct = default);
    Task<ReportTable> ContractorOutstandingAsync(CancellationToken ct = default);
    Task<ReportTable> CustomerOutstandingAsync(CancellationToken ct = default);
    Task<ReportTable> CompanySummaryAsync(CancellationToken ct = default);

    Task<ReportTable> VillaProfitabilityAsync(CancellationToken ct = default);
    Task<ReportTable> BudgetBurnAsync(CancellationToken ct = default);
    Task<ReportTable> SiteSummaryAsync(CancellationToken ct = default);
    Task<ReportTable> ContractorCommitmentAsync(CancellationToken ct = default);
    Task<ReportTable> SupplierOutstandingAsync(CancellationToken ct = default);
}

public class ReportsService(IAppDbContext db) : IReportsService
{
    /// <summary>
    /// The most rows a row-level report will return.
    ///
    /// <para>These reports had no limit at all. Summaries are bounded by the number of villas or
    /// sites and stay small, but the registers — purchases, consumption, stock — return a row per
    /// transaction for all time. Measured against 36,000 inventory rows the consumption register
    /// alone answered with 1.16 MB; a few years of real trading is tens of megabytes, built
    /// entirely in memory on the server and then parsed entirely in memory in the browser.</para>
    ///
    /// <para>Five thousand rows is more than anyone reads on screen and small enough to stay
    /// quick. A caller who genuinely wants everything narrows the date range, which is the
    /// honest way to ask for a large answer.</para>
    /// </summary>
    private const int MaxRows = 5_000;

    /// <summary>Takes one more row than the cap, so "was there more?" needs no second query.</summary>
    private static (List<T> Rows, bool Trimmed) Cap<T>(List<T> rows)
        => rows.Count > MaxRows ? (rows.Take(MaxRows).ToList(), true) : (rows, false);

    private static string? CapNote(bool trimmed, string what)
        => trimmed
            ? $"Showing the most recent {MaxRows:N0} {what}. Narrow the date range to see the rest."
            : null;

    public async Task<ReportTable> InventoryStockAsync(Guid? siteId, CancellationToken ct = default)
    {
        var q = db.InventoryBalances.AsNoTracking();
        if (siteId is not null) q = q.Where(b => b.SiteId == siteId);
        var (data, trimmed) = Cap(await q.OrderBy(b => b.Site.Name).ThenBy(b => b.Material.Name)
            .Select(b => new
            {
                Site = b.Site.Name, b.Material.Code, Material = b.Material.Name, Unit = b.Material.Unit.Code,
                b.Quantity, b.AverageRate, b.Value, b.Material.MinStockLevel,
                Low = b.Material.MinStockLevel > 0 && b.Quantity <= b.Material.MinStockLevel
            }).Take(MaxRows + 1).ToListAsync(ct));
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Site, x.Code, x.Material, x.Unit, x.Quantity, x.AverageRate, x.Value, x.MinStockLevel, x.Low ? "LOW" : "" }).ToList();
        return new("Inventory Stock",
            ["Site", "Code", "Material", "Unit", "Qty", "Avg Rate", "Value", "Min Level", "Alert"], rows,
            CapNote(trimmed, "stock lines"));
    }

    public async Task<ReportTable> PurchaseRegisterAsync(DateOnly? from, DateOnly? to, Guid? siteId, CancellationToken ct = default)
    {
        var q = db.PurchaseHeaders.AsNoTracking().Where(p => p.Status == TransactionStatus.Posted);
        if (from is not null) q = q.Where(p => p.Date >= from);
        if (to is not null) q = q.Where(p => p.Date <= to);
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        var (data, trimmed) = Cap(await q.OrderByDescending(p => p.Date)
            .Select(p => new
            {
                p.Date, p.TxnNumber, Supplier = p.Supplier.Name, Site = p.Site.Name, Invoice = p.InvoiceNumber ?? "",
                p.SubTotal, p.TaxAmount, p.TotalAmount, p.PaidAmount, p.BalanceAmount
            }).Take(MaxRows + 1).ToListAsync(ct));
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Date, x.TxnNumber, x.Supplier, x.Site, x.Invoice, x.SubTotal, x.TaxAmount, x.TotalAmount, x.PaidAmount, x.BalanceAmount }).ToList();
        return new("Purchase Register",
            ["Date", "Txn", "Supplier", "Site", "Invoice", "Sub Total", "Tax", "Total", "Paid", "Balance"], rows,
            CapNote(trimmed, "purchases"));
    }

    public async Task<ReportTable> ConsumptionRegisterAsync(DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct = default)
    {
        var q = db.InventoryTransactions.AsNoTracking()
            .Where(t => t.Type == InventoryTransactionType.ProjectConsumption);
        if (from is not null) q = q.Where(t => t.Date >= from);
        if (to is not null) q = q.Where(t => t.Date <= to);
        if (projectId is not null) q = q.Where(t => t.ProjectId == projectId);
        var (data, trimmed) = Cap(await q.OrderByDescending(t => t.Date)
            .Select(t => new
            {
                t.Date, t.TxnNumber, Project = t.Project != null ? t.Project.Name : "", Material = t.Material.Name,
                t.Quantity, t.Rate, t.Amount, Request = t.SourceRef ?? ""
            }).Take(MaxRows + 1).ToListAsync(ct));
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Date, x.TxnNumber, x.Project, x.Material, -x.Quantity, x.Rate, -x.Amount, x.Request }).ToList();
        return new("Consumption Register",
            ["Date", "Txn", "Project", "Material", "Qty", "Rate", "Value", "Request"], rows,
            CapNote(trimmed, "issues"));
    }

    public async Task<ReportTable> LowStockAsync(CancellationToken ct = default)
    {
        var data = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.Material.MinStockLevel > 0 && b.Quantity <= b.Material.MinStockLevel)
            .OrderBy(b => b.Site.Name).ThenBy(b => b.Material.Name)
            .Select(b => new
            {
                Site = b.Site.Name, Material = b.Material.Name, Unit = b.Material.Unit.Code,
                b.Quantity, b.Material.MinStockLevel, b.Material.ReorderLevel
            }).ToListAsync(ct);
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Site, x.Material, x.Unit, x.Quantity, x.MinStockLevel, x.ReorderLevel }).ToList();
        return new("Low Stock", ["Site", "Material", "Unit", "Qty", "Min Level", "Reorder Level"], rows);
    }

    public async Task<ReportTable> ProjectCostSummaryAsync(CancellationToken ct = default)
    {
        var projects = await db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Name, SiteName = p.Site.Name, p.EstimatedCost, p.ContractSaleValue, p.Status })
            .ToListAsync(ct);

        var costs = await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.ProjectId)
            .Select(g => new { ProjectId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Total, ct);

        var rows = projects.Select(p =>
        {
            var actual = costs.GetValueOrDefault(p.Id, 0m);
            var margin = p.ContractSaleValue.HasValue ? p.ContractSaleValue.Value - actual : (decimal?)null;
            return (IReadOnlyList<object?>)new object?[]
                { p.Name, p.SiteName, p.Status.ToString(), p.EstimatedCost, actual, p.EstimatedCost - actual, p.ContractSaleValue, margin };
        }).ToList();
        return new("Project Cost Summary",
            ["Project", "Site", "Status", "Estimated", "Actual", "Variance", "Sale Value", "Margin"], rows);
    }

    public async Task<ReportTable> ContractorOutstandingAsync(CancellationToken ct = default)
    {
        var data = await db.ContractWorks.AsNoTracking()
            .GroupBy(w => new { w.ContractorId, w.Contractor.Name })
            .Select(g => new
            {
                g.Key.Name,
                Contracted = g.Sum(x => x.ContractAmount),
                Paid = g.Sum(x => x.TotalPaid),
                Balance = g.Sum(x => x.Balance)
            }).OrderByDescending(x => x.Balance).ToListAsync(ct);
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[] { x.Name, x.Contracted, x.Paid, x.Balance }).ToList();
        return new("Contractor Outstanding", ["Contractor", "Contracted", "Paid", "Outstanding"], rows);
    }

    public async Task<ReportTable> CustomerOutstandingAsync(CancellationToken ct = default)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.CustomerId != null)
            .GroupBy(p => new { p.CustomerId, Name = p.Customer!.Name })
            .Select(g => new { g.Key.CustomerId, g.Key.Name, Sale = g.Sum(x => x.ContractSaleValue ?? 0m) })
            .ToListAsync(ct);
        var receipts = await db.CustomerPayments.AsNoTracking()
            .Where(p => p.Status == TransactionStatus.Posted)
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Received = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Received, ct);

        var rows = projects.Select(p =>
        {
            var recv = receipts.GetValueOrDefault(p.CustomerId!.Value, 0m);
            return (IReadOnlyList<object?>)new object?[] { p.Name, p.Sale, recv, p.Sale - recv };
        }).OrderByDescending(r => (decimal)r[3]!).ToList();
        return new("Customer Outstanding", ["Customer", "Sale Value", "Received", "Outstanding"], rows);
    }

    public async Task<ReportTable> CompanySummaryAsync(CancellationToken ct = default)
    {
        async Task<decimal> ExpenseBy(Func<IQueryable<Domain.Entities.ProjectExpense>, IQueryable<Domain.Entities.ProjectExpense>> filter)
            => await filter(db.ProjectExpenses.Where(e => e.Status == TransactionStatus.Posted)).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var totalPurchase = await db.PurchaseHeaders.Where(p => p.Status == TransactionStatus.Posted).SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0m;
        var inventoryValue = await db.InventoryBalances.SumAsync(b => (decimal?)b.Value, ct) ?? 0m;
        var consumption = await ExpenseBy(q => q.Where(e => e.ExpenseType == ProjectExpenseType.Material));
        var labour = await ExpenseBy(q => q.Where(e => e.ExpenseType == ProjectExpenseType.Labour));
        var contractor = await ExpenseBy(q => q.Where(e => e.ExpenseType == ProjectExpenseType.Contractor));
        var otherExp = await ExpenseBy(q => q.Where(e => e.ExpenseType != ProjectExpenseType.Material
            && e.ExpenseType != ProjectExpenseType.Labour && e.ExpenseType != ProjectExpenseType.Contractor));
        var projectCost = consumption + labour + contractor + otherExp;
        var siteOverhead = await db.SiteExpenses.Where(e => e.Status == TransactionStatus.Posted)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var receipts = await db.CustomerPayments.Where(p => p.Status == TransactionStatus.Posted).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var saleValue = await db.Projects.SumAsync(p => (decimal?)(p.ContractSaleValue ?? 0m), ct) ?? 0m;
        var contractorPayable = await db.ContractWorks.SumAsync(w => (decimal?)w.Balance, ct) ?? 0m;
        var supplierPayable = await db.PurchaseHeaders.Where(p => p.Status == TransactionStatus.Posted).SumAsync(p => (decimal?)p.BalanceAmount, ct) ?? 0m;

        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Total purchase (posted)", totalPurchase },
            new object?[] { "Inventory value (in stock)", inventoryValue },
            new object?[] { "Material consumed (project cost)", consumption },
            new object?[] { "Labour cost", labour },
            new object?[] { "Contractor cost", contractor },
            new object?[] { "Other project expenses", otherExp },
            new object?[] { "Total project cost", projectCost },
            new object?[] { "Site overhead (not on any villa)", siteOverhead },
            new object?[] { "Total cost incl. site overhead", projectCost + siteOverhead },
            new object?[] { "Customer sale value", saleValue },
            new object?[] { "Customer receipts", receipts },
            new object?[] { "Customer outstanding", saleValue - receipts },
            new object?[] { "Contractor payable", contractorPayable },
            new object?[] { "Supplier payable", supplierPayable },
        };
        return new("Company Summary", ["Metric", "Amount"], rows);
    }

    // ── the five that answer "how are we actually doing" ────────────────────

    /// <summary>Posted cost per project, the number every report below is built on.</summary>
    private async Task<Dictionary<Guid, decimal>> CostByProjectAsync(CancellationToken ct) =>
        await db.ProjectExpenses.AsNoTracking()
            .Where(e => e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.ProjectId)
            .Select(g => new { ProjectId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Total, ct);

    private async Task<Dictionary<Guid, decimal>> ReceiptsByProjectAsync(CancellationToken ct) =>
        await db.CustomerPayments.AsNoTracking()
            .Where(p => p.Status == TransactionStatus.Posted)
            .GroupBy(p => p.ProjectId)
            .Select(g => new { ProjectId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Total, ct);

    /// <summary>
    /// Profit per villa, credited against the part of the sale actually built.
    ///
    /// The margin elsewhere in the app is sale price minus cost so far, which on a half-built villa
    /// reports the whole sale value against half the cost and shows a profit nobody has earned. Here
    /// revenue is recognised in proportion to <c>CompletionPercent</c>, so a villa at 50% carries half
    /// its sale value. The contracted value stays on the sheet — it is the right number for the sales
    /// pipeline, just not for profit.
    /// </summary>
    public async Task<ReportTable> VillaProfitabilityAsync(CancellationToken ct = default)
    {
        var projects = await db.Projects.AsNoTracking()
            .OrderBy(p => p.Site.Name).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, Site = p.Site.Name, p.CompletionPercent, p.Status,
                p.ContractSaleValue, Customer = p.Customer != null ? p.Customer.Name : null,
            }).ToListAsync(ct);

        var costs = await CostByProjectAsync(ct);
        var receipts = await ReceiptsByProjectAsync(ct);

        var rows = projects.Select(p =>
        {
            var cost = costs.GetValueOrDefault(p.Id, 0m);
            var sale = p.ContractSaleValue;
            var earned = sale.HasValue ? Math.Round(sale.Value * p.CompletionPercent / 100m, 2) : (decimal?)null;
            var margin = earned.HasValue ? earned.Value - cost : (decimal?)null;
            var received = receipts.GetValueOrDefault(p.Id, 0m);
            var outstanding = sale.HasValue ? sale.Value - received : 0m;

            // A handed-over villa with money still owed is the most urgent line on this sheet.
            var flag = p.Status == ProjectStatus.Completed && outstanding > 0 ? "DUES ON HANDOVER"
                : sale is null ? "unsold"
                : "";

            return (IReadOnlyList<object?>)new object?[]
            {
                p.Name, p.Site, p.Customer ?? "", p.CompletionPercent, cost,
                sale, earned, margin, received, sale.HasValue ? outstanding : null, flag,
            };
        }).ToList();

        return new("Villa Profitability",
            ["Villa", "Site", "Customer", "% Done", "Cost To Date", "Contracted Sale",
             "Earned Revenue", "Earned Margin", "Received", "Outstanding", "Flag"], rows);
    }

    /// <summary>
    /// Spend against the part of the work actually done.
    ///
    /// The plain estimate-minus-actual variance shows a large positive number on every half-built
    /// villa, which reads as money saved when it is really a house that is not finished. Burn compares
    /// what has been spent with what the estimate says should have been spent by this stage — over
    /// 100% means the villa is already running hot, however early it is.
    /// </summary>
    public async Task<ReportTable> BudgetBurnAsync(CancellationToken ct = default)
    {
        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.Status != ProjectStatus.Cancelled)
            .OrderBy(p => p.Site.Name).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, Site = p.Site.Name, p.EstimatedCost, p.CompletionPercent, p.Status })
            .ToListAsync(ct);

        var costs = await CostByProjectAsync(ct);

        var rows = projects.Select(p =>
        {
            var spent = costs.GetValueOrDefault(p.Id, 0m);
            var expected = Math.Round(p.EstimatedCost * p.CompletionPercent / 100m, 2);
            // Nothing built yet means nothing to compare against — an unstarted villa is not "over".
            var burn = expected > 0 ? Math.Round(spent / expected * 100m, 0) : (decimal?)null;
            var flag = burn is null ? "not started"
                : burn > 110 ? "OVER BUDGET"
                : burn > 100 ? "watch"
                : "";
            var toComplete = Math.Max(0m, p.EstimatedCost - spent);

            return (IReadOnlyList<object?>)new object?[]
                { p.Name, p.Site, p.CompletionPercent, p.EstimatedCost, expected, spent, burn, toComplete, flag };
        }).ToList();

        return new("Budget vs Progress",
            ["Villa", "Site", "% Done", "Estimate", "Expected By Now", "Spent", "Burn %", "Left In Budget", "Flag"], rows);
    }

    /// <summary>Everything tied up in one site: what has been built, what is on its shelves, what is owed to it.</summary>
    public async Task<ReportTable> SiteSummaryAsync(CancellationToken ct = default)
    {
        var sites = await db.Sites.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Status }).ToListAsync(ct);

        var projects = await db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.SiteId, p.ContractSaleValue, p.Status }).ToListAsync(ct);

        var stock = await db.InventoryBalances.AsNoTracking()
            .GroupBy(b => b.SiteId)
            .Select(g => new { SiteId = g.Key, Value = g.Sum(x => x.Value) })
            .ToDictionaryAsync(x => x.SiteId, x => x.Value, ct);

        var overhead = await db.SiteExpenses.AsNoTracking()
            .Where(e => e.Status == TransactionStatus.Posted)
            .GroupBy(e => e.SiteId)
            .Select(g => new { SiteId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SiteId, x => x.Total, ct);

        var costs = await CostByProjectAsync(ct);
        var receipts = await ReceiptsByProjectAsync(ct);

        var rows = sites.Select(s =>
        {
            var mine = projects.Where(p => p.SiteId == s.Id).ToList();
            var built = mine.Sum(p => costs.GetValueOrDefault(p.Id, 0m));
            var sale = mine.Sum(p => p.ContractSaleValue ?? 0m);
            var recd = mine.Sum(p => receipts.GetValueOrDefault(p.Id, 0m));
            var onShelf = stock.GetValueOrDefault(s.Id, 0m);
            var siteOverhead = overhead.GetValueOrDefault(s.Id, 0m);
            var unsold = mine.Count(p => p.ContractSaleValue is null);

            return (IReadOnlyList<object?>)new object?[]
            {
                s.Name, s.Status.ToString(), mine.Count, unsold,
                built, siteOverhead, onShelf, built + siteOverhead + onShelf, sale, recd, sale - recd,
            };
        }).ToList();

        return new("Site Summary",
            ["Site", "Status", "Villas", "Unsold", "Villa Cost", "Site Overhead", "Stock Value",
             "Capital Employed", "Sale Value", "Received", "Outstanding"], rows);
    }

    /// <summary>
    /// What has been promised to contractors but not yet paid.
    ///
    /// A villa's cost counts money already paid out. The balance on its open work orders is money the
    /// company is committed to and has not spent — invisible on the cost sheet, and the difference
    /// between a villa that looks affordable to finish and one that is not.
    /// </summary>
    public async Task<ReportTable> ContractorCommitmentAsync(CancellationToken ct = default)
    {
        var data = await db.ContractWorks.AsNoTracking()
            .Where(w => w.WorkStatus != ContractWorkStatus.Cancelled)
            .OrderBy(w => w.Project.Site.Name).ThenBy(w => w.Project.Name).ThenBy(w => w.WorkCategory)
            .Select(w => new
            {
                Site = w.Project.Site.Name, Project = w.Project.Name, Contractor = w.Contractor.Name,
                w.WorkCategory, w.ContractAmount, w.TotalPaid, w.Balance, w.WorkStatus,
            }).ToListAsync(ct);

        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
        {
            x.Project, x.Site, x.Contractor, x.WorkCategory, x.WorkStatus.ToString(),
            x.ContractAmount, x.TotalPaid, x.Balance,
        }).ToList();

        return new("Contractor Commitment",
            ["Villa", "Site", "Contractor", "Work", "Status", "Contracted", "Paid", "Committed Unpaid"], rows);
    }

    /// <summary>What is owed to suppliers, by supplier. The other half of the payables picture.</summary>
    public async Task<ReportTable> SupplierOutstandingAsync(CancellationToken ct = default)
    {
        var data = await db.PurchaseHeaders.AsNoTracking()
            .Where(p => p.Status == TransactionStatus.Posted)
            .GroupBy(p => new { p.SupplierId, Name = p.Supplier.Name })
            .Select(g => new
            {
                g.Key.Name,
                Bills = g.Count(),
                Invoiced = g.Sum(x => x.TotalAmount),
                Paid = g.Sum(x => x.PaidAmount),
                Balance = g.Sum(x => x.BalanceAmount),
            }).ToListAsync(ct);

        var rows = data.OrderByDescending(x => x.Balance)
            .Select(x => (IReadOnlyList<object?>)new object?[] { x.Name, x.Bills, x.Invoiced, x.Paid, x.Balance })
            .ToList();

        return new("Supplier Outstanding", ["Supplier", "Bills", "Invoiced", "Paid", "Outstanding"], rows);
    }
}
