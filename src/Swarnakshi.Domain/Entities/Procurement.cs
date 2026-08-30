using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class PurchaseHeader : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;

    /// <summary>Informational only — a purchase feeds site inventory, not a project directly.</summary>
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public MaterialRequest? MaterialRequest { get; set; }

    public string? InvoiceNumber { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly Date { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
}

public class PurchaseItem : BaseEntity
{
    public Guid PurchaseHeaderId { get; set; }
    public PurchaseHeader Header { get; set; } = null!;
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SupplierPayment : BaseEntity
{
    public Guid PurchaseHeaderId { get; set; }
    public PurchaseHeader Header { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Reference { get; set; }
}

public class MaterialRequest : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public MaterialRequestType RequestType { get; set; } = MaterialRequestType.FromStock;
    public MaterialRequestStatus RequestStatus { get; set; } = MaterialRequestStatus.Draft;
    public Guid RequestedByUserId { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }

    public ICollection<MaterialRequestItem> Items { get; set; } = new List<MaterialRequestItem>();
}

public class MaterialRequestItem : BaseEntity
{
    public Guid MaterialRequestId { get; set; }
    public MaterialRequest Request { get; set; } = null!;
    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;
    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public decimal RequestedQty { get; set; }
    public decimal? ApprovedQty { get; set; }
    public decimal IssuedQty { get; set; }
    public decimal? Rate { get; set; }

    /// <summary>Which project expense head/subhead the consumption is booked against.</summary>
    public Guid? ExpenseHeadId { get; set; }
    public ExpenseHead? ExpenseHead { get; set; }
    public Guid? ExpenseSubheadId { get; set; }
    public ExpenseSubhead? ExpenseSubhead { get; set; }
}
