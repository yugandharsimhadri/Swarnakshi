using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class ContractWork : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;

    public string WorkCategory { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>What was agreed with the contractor. The only figure a payment is measured against.</summary>
    public decimal ContractAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ExpectedCompletion { get; set; }
    public DateOnly? ActualCompletion { get; set; }
    public string? PaymentTerms { get; set; }
    public ContractWorkStatus WorkStatus { get; set; } = ContractWorkStatus.Planned;

    public decimal TotalPaid { get; set; }
    public decimal Balance { get; set; }

    public ICollection<ContractorPayment> Payments { get; set; } = new List<ContractorPayment>();
}

public class ContractorPayment : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? ContractWorkId { get; set; }
    public ContractWork? ContractWork { get; set; }

    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public ContractorPaymentKind PaymentKind { get; set; } = ContractorPaymentKind.Partial;
}

/// <summary>
/// A request to correct or remove a contractor payment that has already been posted.
///
/// <para>A posted payment has moved money: it has raised the contract's paid figure and charged the
/// villa. Editing it in place would rewrite all of that with nobody asked, so the payment is left
/// exactly as it is and the person who spotted the mistake — usually the Engineer — raises one of
/// these instead. It carries the proposed values and waits in the Owner's queue; only when it is
/// approved is the payment changed and the contract and the villa's cost brought into line, in one
/// transaction. Until then the books still say what they said yesterday.</para>
///
/// <para>For a <see cref="ContractorPaymentChangeKind.Delete"/> the proposed values are simply a copy
/// of the payment as it stands, so the Owner is shown what is going to disappear.</para>
/// </summary>
public class ContractorPaymentChange : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid ContractorPaymentId { get; set; }
    public ContractorPayment ContractorPayment { get; set; } = null!;
    public ContractorPaymentChangeKind Kind { get; set; }

    /// <summary>Why. Required: an Owner asked to approve a change to money already paid deserves to be told.</summary>
    public string Reason { get; set; } = null!;

    // What the payment looked like when the change was asked for. The payment cannot move while a
    // change is pending, so this is also what the Owner is comparing against.
    public decimal PreviousAmount { get; set; }
    public DateOnly PreviousDate { get; set; }

    // What it should become.
    public Guid? ContractWorkId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid PaymentMethodId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public ContractorPaymentKind PaymentKind { get; set; }
}
