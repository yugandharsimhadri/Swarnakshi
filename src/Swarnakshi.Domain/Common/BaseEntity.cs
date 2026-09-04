using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Common;

/// <summary>
/// Marks a row as belonging to exactly one tenant company. Every tenant row carries this, and
/// <c>AppDbContext</c> both stamps it on insert and filters every read by it — so a query that
/// forgets the tenant cannot leak another company's data.
/// </summary>
public interface ITenantOwned
{
    Guid CompanyId { get; set; }
}

/// <summary>Base for rows that live ABOVE tenancy: the companies themselves and platform operators.</summary>
public abstract class PlatformEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class BaseEntity : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owning tenant. Stamped automatically on insert; never set it by hand in a service.</summary>
    public Guid CompanyId { get; set; }

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

    /// <summary>Optimistic-concurrency token. Regenerated on every save, in the application rather
    /// than by the database, so the rule is the same wherever the row is written.</summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
