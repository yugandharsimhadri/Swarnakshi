using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

/// <summary>
/// A person on the company's payroll — a supervisor, storekeeper, driver, office staff.
///
/// Distinct from <see cref="LabourEntry"/> on purpose: that records daily site labour as a cost by
/// category with no worker master, which is how gangs are actually engaged. This is the small number
/// of named people who draw a monthly salary and take advances against it.
/// </summary>
public class Employee : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Mandatory — on site it is the only reliable way to reach someone.</summary>
    public string Phone { get; set; } = null!;

    /// <summary>Agreed monthly salary. The reference figure; what is actually paid is each payment.</summary>
    public decimal MonthlySalary { get; set; }

    public DateOnly JoinDate { get; set; }
    public DateOnly? LeaveDate { get; set; }

    public string? Designation { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Optional home site. An employee is a company record, not a site-owned one.</summary>
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }

    public ICollection<EmployeePayment> Payments { get; set; } = new List<EmployeePayment>();
}

/// <summary>
/// One payment to an employee. Salary, an advance against future salary, a bonus or a reimbursement.
///
/// A salary payment may recover part of an outstanding advance, so the employee ledger is
/// advances given − advances recovered = still outstanding.
/// </summary>
public class EmployeePayment : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly Date { get; set; }
    public EmployeePaymentKind Kind { get; set; } = EmployeePaymentKind.Salary;

    /// <summary>Gross amount of this payment.</summary>
    public decimal Amount { get; set; }

    /// <summary>Advance settled by this salary payment. Net handed over = Amount − AdvanceRecovered.</summary>
    public decimal AdvanceRecovered { get; set; }

    /// <summary>The month (or span) a salary payment covers. Null for an advance or a bonus.</summary>
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Reference { get; set; }

    /// <summary>
    /// Charge this payment to a project. Left blank it is a company overhead and never reaches
    /// project cost — which is the honest default for office staff.
    /// </summary>
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public decimal NetPaid => Amount - AdvanceRecovered;
}
