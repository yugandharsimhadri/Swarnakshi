using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence.Configurations;

/// <summary>
/// The approval queue, the number sequences behind transaction numbers, and attachments.
/// </summary>

public class ApprovalConfig : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> e)
    {
        e.HasIndex(x => new { x.EntityType, x.EntityId });
        e.HasIndex(x => x.CurrentStatus);
        e.HasMany(x => x.History).WithOne(x => x.Request).HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SequenceConfig : IEntityTypeConfiguration<TransactionSequence>
{
    public void Configure(EntityTypeBuilder<TransactionSequence> e)
        => e.HasIndex(x => new { x.CompanyId, x.Prefix, x.Year }).IsUnique();
}

public class AttachmentConfig : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> e)
        => e.HasIndex(x => new { x.EntityType, x.EntityId });
}
