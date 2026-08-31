using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Attachments;

public record AttachmentDto(Guid Id, string EntityType, Guid EntityId, string FileName, string ContentType,
    long Size, DateTimeOffset CreatedAt);

public interface IAttachmentService
{
    Task<AttachmentDto> UploadAsync(string entityType, Guid entityId, string fileName, string contentType,
        Stream content, CancellationToken ct = default);
    Task<IReadOnlyList<AttachmentDto>> ListAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<(Stream Content, string FileName, string ContentType)> DownloadAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class AttachmentService(IAppDbContext db, IFileStorage storage, ICurrentUser currentUser) : IAttachmentService
{
    private static readonly HashSet<string> Allowed =
        [".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"];
    private const long MaxBytes = 15 * 1024 * 1024;

    public async Task<AttachmentDto> UploadAsync(string entityType, Guid entityId, string fileName, string contentType,
        Stream content, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!Allowed.Contains(ext))
            throw new AppException($"File type '{ext}' is not allowed.", 400);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new AppException("File is empty.", 400);
        if (buffer.Length > MaxBytes) throw new AppException("File exceeds the 15 MB limit.", 400);
        buffer.Position = 0;

        var storagePath = await storage.SaveAsync(buffer, fileName, contentType, ct);
        var attachment = new Attachment
        {
            EntityType = entityType, EntityId = entityId, FileName = Path.GetFileName(fileName),
            ContentType = contentType, Size = buffer.Length, StoragePath = storagePath,
            UploadedByUserId = currentUser.UserId
        };
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);
        return Map(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(string entityType, Guid entityId, CancellationToken ct = default)
        => await db.Attachments.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto(a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.Size, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<(Stream Content, string FileName, string ContentType)> DownloadAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
                         ?? throw new NotFoundException("Attachment", id);
        var stream = await storage.OpenAsync(attachment.StoragePath, ct);
        return (stream, attachment.FileName, attachment.ContentType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct)
                         ?? throw new NotFoundException("Attachment", id);
        await storage.DeleteAsync(attachment.StoragePath, ct);
        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
    }

    private static AttachmentDto Map(Attachment a)
        => new(a.Id, a.EntityType, a.EntityId, a.FileName, a.ContentType, a.Size, a.CreatedAt);
}
