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

    /// <summary>
    /// Set when a person spent their own money and the company owes them it — the engineer who
    /// bought a bag of cement on a Sunday. Null is the ordinary case: the company paid, and there
    /// is nobody to pay back.
    ///
    /// <para>A user rather than an employee, because the claim is made by whoever is signed in, and
    /// not every person with a login is on the payroll.</para>
    /// </summary>
    public Guid? PaidByUserId { get; set; }
    public User? PaidByUser { get; set; }

    public ReimbursementStatus ReimbursementStatus { get; set; } = ReimbursementStatus.None;
}

/// <summary>
/// A change the customer asked for on their villa: an extra point in the kitchen, a different
/// floor, a wall moved. The register of what was asked, what it was expected to cost, and whether
/// it was agreed.
///
/// <para><b>This is not a cost and must never become one.</b> <see cref="ExpectedAmount"/> is what
/// somebody thought the change would come to while standing in the room — an estimate written down
/// so the conversation is not lost. The money actually spent on it arrives the ordinary way, as
/// <see cref="ProjectExpense"/> rows for the materials and labour it took, and those are what the
/// villa is charged. Adding this figure to a cost roll-up would count the same change twice: once
/// as somebody's guess and once as what it really cost.</para>
///
/// <para>Which is why it is its own table with no link into cost, rather than a flag on an
/// expense. There is nothing to exclude from a sum, because nothing sums it.</para>
/// </summary>
public class AddOnRequest : BaseEntity
{
    public string TxnNumber { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>When the customer asked.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// The kind of work — Plumbing, Electrical, Flooring. The expense heads rather than a list of
    /// its own, so an add-on sits under the same name as the expenses that will be booked for it,
    /// and the two can be read side by side without a translation table in between.
    /// </summary>
    public Guid ExpenseHeadId { get; set; }
    public ExpenseHead Head { get; set; } = null!;

    /// <summary>What the customer actually asked for, in words. The point of the record.</summary>
    public string Description { get; set; } = null!;

    /// <summary>What it is expected to come to. An estimate, never a charge.</summary>
    public decimal ExpectedAmount { get; set; }

    public AddOnStatus Status { get; set; } = AddOnStatus.Requested;

    /// <summary>Why it was declined, what was agreed instead — the margin note.</summary>
    public string? Remarks { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
}

/// <summary>
/// One payment that pays a person back for money they spent out of their own pocket, covering as
/// many approved claims as it likes — which is how it actually happens: five bills over a fortnight,
/// one transfer on Saturday.
///
/// <para><b>This is not a cost.</b> The <see cref="ProjectExpense"/> rows are the cost, counted once
/// when each was approved. A settlement is cash leaving the company against a debt already
/// recorded, and nothing here may ever reach a villa's total — that is why there is no ProjectId on
/// it and no expense row written when one is made.</para>
/// </summary>
public class ExpenseSettlement : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;

    /// <summary>The person being paid back.</summary>
    public Guid PaidToUserId { get; set; }
    public User PaidToUser { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>Checked against the sum of the lines rather than trusted.</summary>
    public decimal Amount { get; set; }

    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>UPI reference, cheque number — whatever proves the money moved.</summary>
    public string? Reference { get; set; }

    public ICollection<ExpenseSettlementLine> Lines { get; set; } = new List<ExpenseSettlementLine>();
}

/// <summary>
/// One claim cleared by one settlement, in full. Paying a man part of what he spent is not a thing
/// anyone does, and allowing it would mean every roll-up of "what we still owe" had to subtract
/// part-payments — so a line carries the claim's whole amount and the settlement covers however
/// many claims it needs to.
/// </summary>
public class ExpenseSettlementLine : BaseEntity
{
    public Guid ExpenseSettlementId { get; set; }
    public ExpenseSettlement Settlement { get; set; } = null!;

    public Guid ProjectExpenseId { get; set; }
    public ProjectExpense ProjectExpense { get; set; } = null!;

    public decimal Amount { get; set; }
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
