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

/// <summary>
/// Money spent on a site rather than on any one villa — the watchman, temporary power, the site
/// office, fencing, a supervisor's salary.
///
/// <para>Deliberately a separate table from <see cref="ProjectExpense"/> rather than a nullable
/// ProjectId on it. The invariant that a project's cost is exactly the sum of its ProjectExpense
/// rows is what stops material being double counted, and loosening it to allow orphan rows would
/// put that at risk for the sake of a different kind of cost entirely.</para>
///
/// <para>Site overhead is real money and belongs in the company's totals. Whether to spread it
/// across the site's villas is a reporting decision, taken where the report is built — not by
/// forcing whoever records the watchman's wages to pick a villa he did not work on.</para>
/// </summary>
public class SiteExpense : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public DateOnly Date { get; set; }

    public Guid ExpenseHeadId { get; set; }
    public ExpenseHead Head { get; set; } = null!;

    public string? Description { get; set; }
    public decimal Amount { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Set when the cost came from somewhere else — an employee's salary, for instance.</summary>
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
