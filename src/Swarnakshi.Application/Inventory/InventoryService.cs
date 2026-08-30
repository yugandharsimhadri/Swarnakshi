using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Inventory;

// ---- DTOs ----------------------------------------------------------------
public record InventoryBalanceDto(Guid MaterialId, string MaterialCode, string MaterialName,
    string CategoryName, string UnitCode, decimal Quantity, decimal AverageRate, decimal Value,
    decimal MinStockLevel, decimal ReorderLevel, decimal? LastPurchaseRate, bool LowStock);

public record InventoryTxnDto(Guid Id, string TxnNumber, DateOnly Date, InventoryTransactionType Type,
    string MaterialName, string UnitCode, decimal Quantity, decimal Rate, decimal Amount,
    Guid? ProjectId, string? ProjectName, string? SourceType, string? SourceRef, string? Remarks);

public record MaterialInventoryDetail(Guid SiteId, Guid MaterialId, string MaterialName, string UnitCode,
    decimal Quantity, decimal AverageRate, decimal Value, decimal MinStockLevel,
    decimal? LastPurchaseRate, decimal TotalPurchasedQty, decimal TotalConsumedQty);

public record OpeningStockRequest(Guid SiteId, Guid MaterialId, decimal Quantity, decimal Rate, DateOnly Date, string? Remarks);
public record AdjustmentRequest(Guid SiteId, Guid MaterialId, decimal QuantityDelta, decimal? Rate, DateOnly Date, string Reason);
public record ReturnRequest(Guid SiteId, Guid ProjectId, Guid MaterialId, decimal Quantity, DateOnly Date, string? Remarks);

public class OpeningStockValidator : AbstractValidator<OpeningStockRequest>
{
    public OpeningStockValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public class AdjustmentValidator : AbstractValidator<AdjustmentRequest>
{
    public AdjustmentValidator()
    {
        RuleFor(x => x.QuantityDelta).NotEqual(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

// ---- Internal ops used by approval handlers -----------------------------
public interface IInventoryLedger
{
    /// <summary>Positive movement into a site's stock. Updates the weighted-average balance.</summary>
    Task<InventoryTransaction> ReceiveAsync(Guid siteId, Guid materialId, Guid unitId, decimal qty, decimal rate,
        InventoryTransactionType type, DateOnly date, string sourceType, Guid sourceId, string? sourceRef,
        Guid? projectId, Guid actorId, CancellationToken ct);

    /// <summary>Negative movement out of a site's stock at the current average (or an explicit rate). Returns the rate used.</summary>
    Task<(InventoryTransaction Txn, decimal RateUsed)> IssueAsync(Guid siteId, Guid materialId, Guid unitId, decimal qty,
        InventoryTransactionType type, DateOnly date, string sourceType, Guid sourceId, string? sourceRef,
        Guid? projectId, decimal? explicitRate, Guid actorId, CancellationToken ct);
}

// ---- Public query + direct ops ----------------------------------------
public interface IInventoryService : IInventoryLedger
{
    Task<IReadOnlyList<InventoryBalanceDto>> BalancesAsync(Guid siteId, Guid? categoryId, bool lowStockOnly, string? q, CancellationToken ct = default);
    Task<MaterialInventoryDetail> MaterialDetailAsync(Guid siteId, Guid materialId, CancellationToken ct = default);
    Task<PagedResult<InventoryTxnDto>> LedgerAsync(PageQuery page, Guid? siteId, Guid? materialId, Guid? projectId, InventoryTransactionType? type, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<InventoryTxnDto> OpeningStockAsync(OpeningStockRequest req, CancellationToken ct = default);
    Task<InventoryTxnDto> AdjustmentAsync(AdjustmentRequest req, CancellationToken ct = default);
    Task<InventoryTxnDto> ReturnFromProjectAsync(ReturnRequest req, CancellationToken ct = default);
}

public class InventoryService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    ITransactionSequenceService sequences,
    IProjectCostWriter costWriter,
    IValidator<OpeningStockRequest> openingValidator,
    IValidator<AdjustmentRequest> adjustmentValidator) : IInventoryService
{
    // ---- ledger core --------------------------------------------------
    public async Task<InventoryTransaction> ReceiveAsync(Guid siteId, Guid materialId, Guid unitId, decimal qty, decimal rate,
        InventoryTransactionType type, DateOnly date, string sourceType, Guid sourceId, string? sourceRef,
        Guid? projectId, Guid actorId, CancellationToken ct)
    {
        var balance = await GetOrCreateBalanceAsync(siteId, materialId, ct);
        balance.Receive(qty, rate, DateTimeOffset.UtcNow);

        var txn = new InventoryTransaction
        {
            TxnNumber = await sequences.NextAsync("INV", ct),
            Date = date, SiteId = siteId, MaterialId = materialId, UnitId = unitId,
            Quantity = qty, Rate = rate, Amount = Math.Round(qty * rate, 2), Type = type,
            ProjectId = projectId, SourceType = sourceType, SourceId = sourceId, SourceRef = sourceRef,
            Status = TransactionStatus.Posted, ApprovedBy = actorId, ApprovedAt = DateTimeOffset.UtcNow
        };
        db.InventoryTransactions.Add(txn);
        await db.SaveChangesAsync(ct);
        return txn;
    }

    public async Task<(InventoryTransaction Txn, decimal RateUsed)> IssueAsync(Guid siteId, Guid materialId, Guid unitId, decimal qty,
        InventoryTransactionType type, DateOnly date, string sourceType, Guid sourceId, string? sourceRef,
        Guid? projectId, decimal? explicitRate, Guid actorId, CancellationToken ct)
    {
        var balance = await GetOrCreateBalanceAsync(siteId, materialId, ct);
        var allowNegative = await settings.GetBoolAsync(SettingKeys.AllowNegativeStock, siteId, false, ct);

        if (!allowNegative && qty > balance.Quantity)
        {
            var material = await db.Materials.AsNoTracking().FirstAsync(m => m.Id == materialId, ct);
            throw new AppException(
                $"Insufficient stock for {material.Name}: available {balance.Quantity}, requested {qty}.", 409);
        }

        var rateUsed = explicitRate ?? balance.AverageRate;
        if (explicitRate is null)
            balance.Issue(qty, DateTimeOffset.UtcNow, allowNegative);
        else
        {
            // manual/override rate: still reduce quantity, value by explicit amount
            balance.Quantity -= qty;
            balance.Value = Math.Max(0, balance.Value - qty * rateUsed);
            balance.AverageRate = balance.Quantity > 0 ? balance.Value / balance.Quantity : rateUsed;
            balance.LastMovementAt = DateTimeOffset.UtcNow;
        }

        var txn = new InventoryTransaction
        {
            TxnNumber = await sequences.NextAsync("INV", ct),
            Date = date, SiteId = siteId, MaterialId = materialId, UnitId = unitId,
            Quantity = -qty, Rate = rateUsed, Amount = Math.Round(-qty * rateUsed, 2), Type = type,
            ProjectId = projectId, SourceType = sourceType, SourceId = sourceId, SourceRef = sourceRef,
            Status = TransactionStatus.Posted, ApprovedBy = actorId, ApprovedAt = DateTimeOffset.UtcNow
        };
        db.InventoryTransactions.Add(txn);
        await db.SaveChangesAsync(ct);
        return (txn, rateUsed);
    }

    private async Task<InventoryBalance> GetOrCreateBalanceAsync(Guid siteId, Guid materialId, CancellationToken ct)
    {
        var balance = await db.InventoryBalances
            .FirstOrDefaultAsync(b => b.SiteId == siteId && b.MaterialId == materialId, ct);
        if (balance is null)
        {
            if (!await db.Sites.AnyAsync(s => s.Id == siteId, ct)) throw new NotFoundException("Site", siteId);
            if (!await db.Materials.AnyAsync(m => m.Id == materialId, ct)) throw new NotFoundException("Material", materialId);
            balance = new InventoryBalance { SiteId = siteId, MaterialId = materialId };
            db.InventoryBalances.Add(balance);
        }
        return balance;
    }

    // ---- queries ----------------------------------------------------
    public async Task<IReadOnlyList<InventoryBalanceDto>> BalancesAsync(Guid siteId, Guid? categoryId, bool lowStockOnly, string? q, CancellationToken ct = default)
    {
        var query = db.InventoryBalances.AsNoTracking().Where(b => b.SiteId == siteId);
        if (categoryId is not null)
            query = query.Where(b => b.Material.Subcategory.MaterialCategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(b => b.Material.Name.Contains(q) || b.Material.Code.Contains(q));

        var rows = await query
            .OrderBy(b => b.Material.Name)
            .Select(b => new InventoryBalanceDto(
                b.MaterialId, b.Material.Code, b.Material.Name, b.Material.Subcategory.Category.Name,
                b.Material.Unit.Code, b.Quantity, b.AverageRate, b.Value,
                b.Material.MinStockLevel, b.Material.ReorderLevel, b.LastPurchaseRate,
                b.Material.MinStockLevel > 0 && b.Quantity <= b.Material.MinStockLevel))
            .ToListAsync(ct);

        return lowStockOnly ? rows.Where(r => r.LowStock).ToList() : rows;
    }

    public async Task<MaterialInventoryDetail> MaterialDetailAsync(Guid siteId, Guid materialId, CancellationToken ct = default)
    {
        var balance = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.SiteId == siteId && b.MaterialId == materialId)
            .Select(b => new { b.Quantity, b.AverageRate, b.Value, b.LastPurchaseRate })
            .FirstOrDefaultAsync(ct);

        var material = await db.Materials.AsNoTracking()
            .Where(m => m.Id == materialId)
            .Select(m => new { m.Name, UnitCode = m.Unit.Code, m.MinStockLevel })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Material", materialId);

        var txns = db.InventoryTransactions.AsNoTracking().Where(t => t.SiteId == siteId && t.MaterialId == materialId);
        var purchased = await txns.Where(t => t.Quantity > 0 && t.Type == InventoryTransactionType.PurchaseReceipt)
            .SumAsync(t => (decimal?)t.Quantity, ct) ?? 0m;
        var consumed = await txns.Where(t => t.Type == InventoryTransactionType.ProjectConsumption)
            .SumAsync(t => (decimal?)(-t.Quantity), ct) ?? 0m;

        return new MaterialInventoryDetail(siteId, materialId, material.Name, material.UnitCode,
            balance?.Quantity ?? 0m, balance?.AverageRate ?? 0m, balance?.Value ?? 0m,
            material.MinStockLevel, balance?.LastPurchaseRate, purchased, consumed);
    }

    public async Task<PagedResult<InventoryTxnDto>> LedgerAsync(PageQuery page, Guid? siteId, Guid? materialId, Guid? projectId,
        InventoryTransactionType? type, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var q = db.InventoryTransactions.AsNoTracking();
        if (siteId is not null) q = q.Where(t => t.SiteId == siteId);
        if (materialId is not null) q = q.Where(t => t.MaterialId == materialId);
        if (projectId is not null) q = q.Where(t => t.ProjectId == projectId);
        if (type is not null) q = q.Where(t => t.Type == type);
        if (from is not null) q = q.Where(t => t.Date >= from);
        if (to is not null) q = q.Where(t => t.Date <= to);
        return await q.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
            .Select(TxnProjection).ToPagedAsync(page, ct);
    }

    // ---- direct ops ------------------------------------------------
    public async Task<InventoryTxnDto> OpeningStockAsync(OpeningStockRequest req, CancellationToken ct = default)
    {
        await openingValidator.ValidateAndThrowAsync(req, ct);
        var unitId = await MaterialUnitAsync(req.MaterialId, ct);
        await using var txn = await db.Database.BeginTransactionAsync(ct);
        var t = await ReceiveAsync(req.SiteId, req.MaterialId, unitId, req.Quantity, req.Rate,
            InventoryTransactionType.OpeningStock, req.Date, "OpeningStock", Guid.Empty, null, null,
            currentUser.UserId!.Value, ct);
        t.Remarks = req.Remarks;
        await db.SaveChangesAsync(ct);
        await txn.CommitAsync(ct);
        return await LoadTxnAsync(t.Id, ct);
    }

    public async Task<InventoryTxnDto> AdjustmentAsync(AdjustmentRequest req, CancellationToken ct = default)
    {
        await adjustmentValidator.ValidateAndThrowAsync(req, ct);

        var needsApproval = await settings.GetBoolAsync(SettingKeys.InventoryAdjustmentNeedsApproval, req.SiteId, true, ct);
        if (needsApproval && !currentUser.Has(Permissions.ApprovalsDecide))
            throw new ForbiddenException("Inventory adjustments require Owner approval — ask an Owner to post it.");

        var unitId = await MaterialUnitAsync(req.MaterialId, ct);
        await using var dbtxn = await db.Database.BeginTransactionAsync(ct);

        InventoryTransaction t;
        if (req.QuantityDelta > 0)
        {
            var rate = req.Rate ?? await CurrentRateAsync(req.SiteId, req.MaterialId, ct);
            t = await ReceiveAsync(req.SiteId, req.MaterialId, unitId, req.QuantityDelta, rate,
                InventoryTransactionType.Adjustment, req.Date, "Adjustment", Guid.Empty, null, null,
                currentUser.UserId!.Value, ct);
        }
        else
        {
            (t, _) = await IssueAsync(req.SiteId, req.MaterialId, unitId, -req.QuantityDelta,
                InventoryTransactionType.Adjustment, req.Date, "Adjustment", Guid.Empty, null, null,
                req.Rate, currentUser.UserId!.Value, ct);
        }
        t.Remarks = req.Reason;
        await db.SaveChangesAsync(ct);
        await dbtxn.CommitAsync(ct);
        return await LoadTxnAsync(t.Id, ct);
    }

    public async Task<InventoryTxnDto> ReturnFromProjectAsync(ReturnRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new AppException("Return quantity must be positive.", 400);
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct)
                      ?? throw new NotFoundException("Project", req.ProjectId);
        if (project.SiteId != req.SiteId)
            throw new AppException("Project does not belong to this site.", 409);

        var unitId = await MaterialUnitAsync(req.MaterialId, ct);
        var rate = await CurrentRateAsync(req.SiteId, req.MaterialId, ct);

        await using var dbtxn = await db.Database.BeginTransactionAsync(ct);
        var t = await ReceiveAsync(req.SiteId, req.MaterialId, unitId, req.Quantity, rate,
            InventoryTransactionType.ReturnFromProject, req.Date, "Return", Guid.Empty, null, req.ProjectId,
            currentUser.UserId!.Value, ct);
        t.Remarks = req.Remarks;

        // reverse the project material cost
        await costWriter.WriteMaterialCostAsync(req.ProjectId, -Math.Round(req.Quantity * rate, 2), req.Date,
            null, null, "InventoryTransaction", t.Id, $"Return: {t.TxnNumber}", ct);

        await db.SaveChangesAsync(ct);
        await dbtxn.CommitAsync(ct);
        return await LoadTxnAsync(t.Id, ct);
    }

    // ---- helpers --------------------------------------------------
    private async Task<Guid> MaterialUnitAsync(Guid materialId, CancellationToken ct)
        => await db.Materials.Where(m => m.Id == materialId).Select(m => m.UnitId).FirstOrDefaultAsync(ct) is var u && u != Guid.Empty
            ? u : throw new NotFoundException("Material", materialId);

    private async Task<decimal> CurrentRateAsync(Guid siteId, Guid materialId, CancellationToken ct)
    {
        var avg = await db.InventoryBalances.AsNoTracking()
            .Where(b => b.SiteId == siteId && b.MaterialId == materialId)
            .Select(b => (decimal?)b.AverageRate).FirstOrDefaultAsync(ct);
        if (avg is > 0) return avg.Value;
        return await db.Materials.Where(m => m.Id == materialId).Select(m => m.DefaultPurchaseRate).FirstAsync(ct);
    }

    private async Task<InventoryTxnDto> LoadTxnAsync(Guid id, CancellationToken ct)
        => await db.InventoryTransactions.AsNoTracking().Where(t => t.Id == id).Select(TxnProjection).FirstAsync(ct);

    private static readonly Expression<Func<InventoryTransaction, InventoryTxnDto>> TxnProjection =
        t => new InventoryTxnDto(t.Id, t.TxnNumber, t.Date, t.Type, t.Material.Name, t.Unit.Code,
            t.Quantity, t.Rate, t.Amount, t.ProjectId, t.Project != null ? t.Project.Name : null,
            t.SourceType, t.SourceRef, t.Remarks);
}
