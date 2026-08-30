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
    public decimal EstimatedCost { get; set; }
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
