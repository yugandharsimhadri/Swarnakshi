using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

/// <summary>Current stock of one material at one site. Maintained from the transaction ledger — never edited directly.</summary>
public class InventoryBalance : BaseEntity
{
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal AverageRate { get; set; }
    public decimal Value { get; set; }

    public DateTimeOffset? LastMovementAt { get; set; }
    public decimal? LastPurchaseRate { get; set; }

    /// <summary>Apply a positive receipt using weighted-average valuation.</summary>
    public void Receive(decimal qty, decimal rate, DateTimeOffset at)
    {
        if (qty <= 0) throw new ArgumentOutOfRangeException(nameof(qty));
        Quantity += qty;
        Value += qty * rate;
        AverageRate = Quantity > 0 ? Value / Quantity : 0m;
        LastPurchaseRate = rate;
        LastMovementAt = at;
    }

    /// <summary>Apply an issue/consumption at the current average rate. Returns the rate used.</summary>
    public decimal Issue(decimal qty, DateTimeOffset at, bool allowNegative)
    {
        if (qty <= 0) throw new ArgumentOutOfRangeException(nameof(qty));
        if (!allowNegative && qty > Quantity)
            throw new InvalidOperationException("Insufficient stock.");
        var rate = AverageRate;
        Quantity -= qty;
        Value -= qty * rate;
        if (Quantity <= 0) { Quantity = allowNegative ? Quantity : 0m; Value = Quantity * rate; }
        AverageRate = Quantity > 0 ? Value / Quantity : rate;
        LastMovementAt = at;
        return rate;
    }
}

public class InventoryTransaction : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public DateOnly Date { get; set; }

    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    /// <summary>Signed: positive = into inventory, negative = out.</summary>
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public InventoryTransactionType Type { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Traceability back to the originating document.</summary>
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceRef { get; set; }
}
