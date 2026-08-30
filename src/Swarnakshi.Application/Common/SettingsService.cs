using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Common;

public static class SettingKeys
{
    public const string ValuationMethod = "inventory.valuation_method";
    public const string AllowNegativeStock = "inventory.allow_negative_stock";
    public const string PurchaseNeedsApproval = "purchase.needs_approval";
    public const string InventoryAdjustmentNeedsApproval = "inventory.adjustment_needs_approval";
}

public interface ISettingsService
{
    Task<string?> GetAsync(string key, Guid? siteId = null, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, Guid? siteId = null, bool fallback = false, CancellationToken ct = default);
    Task<InventoryValuationMethod> ValuationMethodAsync(Guid? siteId = null, CancellationToken ct = default);
}

/// <summary>Resolves a setting: per-site value first, then the global default.</summary>
public class SettingsService(IAppDbContext db) : ISettingsService
{
    public async Task<string?> GetAsync(string key, Guid? siteId = null, CancellationToken ct = default)
    {
        var rows = await db.Settings.AsNoTracking()
            .Where(s => s.Key == key && (s.SiteId == null || s.SiteId == siteId))
            .ToListAsync(ct);
        return rows.FirstOrDefault(s => s.SiteId == siteId)?.Value
               ?? rows.FirstOrDefault(s => s.SiteId == null)?.Value;
    }

    public async Task<bool> GetBoolAsync(string key, Guid? siteId = null, bool fallback = false, CancellationToken ct = default)
        => bool.TryParse(await GetAsync(key, siteId, ct), out var v) ? v : fallback;

    public async Task<InventoryValuationMethod> ValuationMethodAsync(Guid? siteId = null, CancellationToken ct = default)
        => Enum.TryParse<InventoryValuationMethod>(await GetAsync(SettingKeys.ValuationMethod, siteId, ct), out var m)
            ? m : InventoryValuationMethod.WeightedAverage;
}
