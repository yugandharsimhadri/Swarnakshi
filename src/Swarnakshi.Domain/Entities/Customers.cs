using Swarnakshi.Domain.Common;

namespace Swarnakshi.Domain.Entities;

public class CustomerPayment : AuditableEntity
{
    public string TxnNumber { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Guid PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public string? Reference { get; set; }
    public string? Description { get; set; }
}
