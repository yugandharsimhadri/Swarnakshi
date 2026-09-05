using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Common;

public static class SettingKeys
{
    public const string ValuationMethod = "inventory.valuation_method";
    public const string AllowNegativeStock = "inventory.allow_negative_stock";

    /// <summary>
    /// The rupee ceiling under which a document approves itself. <b>0 — the default — means nothing
    /// does</b>, so every purchase, expense and payment waits for the Owner.
    ///
    /// <para>There is deliberately no "this type never needs approval" switch beside it. One number
    /// the owner can read off a screen is a rule they can hold in their head; a grid of per-type
    /// booleans is how a company ends up with a category of spending nobody remembers exempting.</para>
    /// </summary>
    public const string AutoApproveLimit = "approvals.auto_approve_limit";
}

public interface ISettingsService
{
    Task<string?> GetAsync(string key, Guid? siteId = null, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, Guid? siteId = null, bool fallback = false, CancellationToken ct = default);
    Task<InventoryValuationMethod> ValuationMethodAsync(Guid? siteId = null, CancellationToken ct = default);

    /// <summary>The auto-approve ceiling in rupees. Never negative; 0 when unset or unreadable.</summary>
    Task<decimal> AutoApproveLimitAsync(Guid? siteId = null, CancellationToken ct = default);

    Task SetAsync(string key, string value, Guid? siteId = null, CancellationToken ct = default);
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

    /// <summary>
    /// Every way of failing to read this lands on 0, which means "approve nothing automatically".
    /// A missing row, a hand-edited value, a negative: the safe reading of all three is that the
    /// owner still wants to see it.
    /// </summary>
    public async Task<decimal> AutoApproveLimitAsync(Guid? siteId = null, CancellationToken ct = default)
    {
        var raw = await GetAsync(SettingKeys.AutoApproveLimit, siteId, ct);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v : 0m;
    }

    /// <summary>
    /// Writes the value invariantly, so a server whose locale uses a comma for the decimal point
    /// cannot store a limit that reads back as a different number.
    /// </summary>
    public async Task SetAsync(string key, string value, Guid? siteId = null, CancellationToken ct = default)
    {
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key && s.SiteId == siteId, ct);
        if (row is null)
            db.Settings.Add(new Setting { Key = key, Value = value, SiteId = siteId });
        else
            row.Value = value;
        await db.SaveChangesAsync(ct);
    }
}
