using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

/// <summary>One row per approvable entity instance currently in / through the approval pipeline.</summary>
public class ApprovalRequest : BaseEntity
{
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string? EntityRef { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal? Amount { get; set; }

    public TransactionStatus CurrentStatus { get; set; } = TransactionStatus.Submitted;
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? Remarks { get; set; }

    public ICollection<ApprovalHistory> History { get; set; } = new List<ApprovalHistory>();
}

public class ApprovalHistory : BaseEntity
{
    public Guid ApprovalRequestId { get; set; }
    public ApprovalRequest Request { get; set; } = null!;
    public ApprovalAction Action { get; set; }
    public TransactionStatus PreviousStatus { get; set; }
    public TransactionStatus NewStatus { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string? Remarks { get; set; }
}

public class AuditLog : BaseEntity
{
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = null!;
    public string? DataJson { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Backs human-readable transaction numbers: {Prefix}-{Year}-{LastNumber:00000}.</summary>
public class TransactionSequence : BaseEntity
{
    public string Prefix { get; set; } = null!;
    public int Year { get; set; }
    public int LastNumber { get; set; }
}

public class Attachment : BaseEntity
{
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Size { get; set; }
    public string StoragePath { get; set; } = null!;
    public Guid? UploadedByUserId { get; set; }
}
