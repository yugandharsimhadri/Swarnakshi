using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; set; }
    /// <summary>Marks development/demo rows so they can be purged safely.</summary>
    public bool IsDemo { get; set; }
}

/// <summary>Base for transactional entities that carry audit + approval state.</summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Draft;
    public string? Remarks { get; set; }
    public byte[]? RowVersion { get; set; }
}
