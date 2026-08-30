using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

/// <summary>One costed event against a project. Material consumption and posted payments create these automatically.</summary>
public class ProjectExpense : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public DateOnly Date { get; set; }

    public Guid ExpenseHeadId { get; set; }
    public ExpenseHead Head { get; set; } = null!;
    public Guid? ExpenseSubheadId { get; set; }
    public ExpenseSubhead? Subhead { get; set; }

    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public ProjectExpenseType ExpenseType { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Traceability to the source doc (InventoryTransaction, ContractorPayment, LabourEntry, manual).</summary>
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
}

/// <summary>Labour cost by category and period. No individual worker master.</summary>
public class LabourEntry : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid LabourCategoryId { get; set; }
    public LabourCategory LabourCategory { get; set; } = null!;

    public LabourPeriodType PeriodType { get; set; } = LabourPeriodType.Daily;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal Amount { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? PaymentType { get; set; }
    public string? Remarks { get; set; }
}
