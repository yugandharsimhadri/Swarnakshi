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

/// <summary>
/// The audit trail. No screen reads it — it exists so that "who changed this, and what did it say
/// before?" can be answered months later, which is a question nobody can answer retrospectively.
///
/// <para>Written entirely by <c>AppDbContext.SaveChangesAsync</c>, so it covers every write in the
/// application without any service having to remember. It is append-only in practice: nothing in
/// the application updates or deletes a row here.</para>
/// </summary>
public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.Property(x => x.EntityType).HasMaxLength(100);
        e.Property(x => x.Action).HasMaxLength(400);

        // Deliberately unbounded: this holds a field-level diff, or the whole of a row that is
        // being deleted. The 512-character convention every other string gets would silently
        // truncate exactly the rows worth keeping. SaveChangesAsync caps it at a sane size instead.
        e.Property(x => x.DataJson).HasColumnType("nvarchar(max)");

        // The two questions this table is ever asked. Both are covered rather than one, because a
        // trail that is slow to search is a trail nobody searches.
        e.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId, x.At })
            .HasDatabaseName("IX_AuditLogs_Entity");                 // what happened to THIS row
        e.HasIndex(x => new { x.CompanyId, x.At })
            .HasDatabaseName("IX_AuditLogs_When");                   // what happened last Tuesday
    }
}
