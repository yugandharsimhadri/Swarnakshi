using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Reports;

public record ReportTable(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

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
}

public class ReportsService(IAppDbContext db) : IReportsService
{
    public async Task<ReportTable> InventoryStockAsync(Guid? siteId, CancellationToken ct = default)
    {
        var q = db.InventoryBalances.AsNoTracking();
        if (siteId is not null) q = q.Where(b => b.SiteId == siteId);
        var data = await q.OrderBy(b => b.Site.Name).ThenBy(b => b.Material.Name)
            .Select(b => new
            {
                Site = b.Site.Name, b.Material.Code, Material = b.Material.Name, Unit = b.Material.Unit.Code,
                b.Quantity, b.AverageRate, b.Value, b.Material.MinStockLevel,
                Low = b.Material.MinStockLevel > 0 && b.Quantity <= b.Material.MinStockLevel
            }).ToListAsync(ct);
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Site, x.Code, x.Material, x.Unit, x.Quantity, x.AverageRate, x.Value, x.MinStockLevel, x.Low ? "LOW" : "" }).ToList();
        return new("Inventory Stock",
            ["Site", "Code", "Material", "Unit", "Qty", "Avg Rate", "Value", "Min Level", "Alert"], rows);
    }

    public async Task<ReportTable> PurchaseRegisterAsync(DateOnly? from, DateOnly? to, Guid? siteId, CancellationToken ct = default)
    {
        var q = db.PurchaseHeaders.AsNoTracking().Where(p => p.Status == TransactionStatus.Posted);
        if (from is not null) q = q.Where(p => p.Date >= from);
        if (to is not null) q = q.Where(p => p.Date <= to);
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        var data = await q.OrderByDescending(p => p.Date)
            .Select(p => new
            {
                p.Date, p.TxnNumber, Supplier = p.Supplier.Name, Site = p.Site.Name, Invoice = p.InvoiceNumber ?? "",
                p.SubTotal, p.TaxAmount, p.TotalAmount, p.PaidAmount, p.BalanceAmount
            }).ToListAsync(ct);
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Date, x.TxnNumber, x.Supplier, x.Site, x.Invoice, x.SubTotal, x.TaxAmount, x.TotalAmount, x.PaidAmount, x.BalanceAmount }).ToList();
        return new("Purchase Register",
            ["Date", "Txn", "Supplier", "Site", "Invoice", "Sub Total", "Tax", "Total", "Paid", "Balance"], rows);
    }

    public async Task<ReportTable> ConsumptionRegisterAsync(DateOnly? from, DateOnly? to, Guid? projectId, CancellationToken ct = default)
    {
        var q = db.InventoryTransactions.AsNoTracking()
            .Where(t => t.Type == InventoryTransactionType.ProjectConsumption);
        if (from is not null) q = q.Where(t => t.Date >= from);
        if (to is not null) q = q.Where(t => t.Date <= to);
        if (projectId is not null) q = q.Where(t => t.ProjectId == projectId);
        var data = await q.OrderByDescending(t => t.Date)
            .Select(t => new
            {
                t.Date, t.TxnNumber, Project = t.Project != null ? t.Project.Name : "", Material = t.Material.Name,
                t.Quantity, t.Rate, t.Amount, Request = t.SourceRef ?? ""
            }).ToListAsync(ct);
        var rows = data.Select(x => (IReadOnlyList<object?>)new object?[]
            { x.Date, x.TxnNumber, x.Project, x.Material, -x.Quantity, x.Rate, -x.Amount, x.Request }).ToList();
        return new("Consumption Register",
            ["Date", "Txn", "Project", "Material", "Qty", "Rate", "Value", "Request"], rows);
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
            new object?[] { "Customer sale value", saleValue },
            new object?[] { "Customer receipts", receipts },
            new object?[] { "Customer outstanding", saleValue - receipts },
            new object?[] { "Contractor payable", contractorPayable },
            new object?[] { "Supplier payable", supplierPayable },
        };
        return new("Company Summary", ["Metric", "Amount"], rows);
    }
}
