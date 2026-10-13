using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
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

/// <summary>
/// What may be attached to one kind of document. Per entity type rather than one rule for
/// everything, because the attachments answer different questions: a purchase carries the
/// supplier's invoice, which arrives as a PDF from his accountant, while a villa expense carries
/// the bill the engineer photographed at the counter — and that one is evidence the Owner looks at
/// before paying a man back, so it has to be an image he can see on a phone, taken small enough to
/// arrive over 4G from a site with one bar of signal.
/// </summary>
public record AttachmentPolicy(string[] Extensions, long MaxBytes, bool ImagesOnly)
{
    public string Limit => MaxBytes >= 1024 * 1024
        ? $"{MaxBytes / 1024m / 1024m:0.#} MB"
        : $"{MaxBytes / 1024} KB";
}

public class AttachmentService(IAppDbContext db, IFileStorage storage, ICurrentUser currentUser) : IAttachmentService
{
    private static readonly AttachmentPolicy Default = new(
        [".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"],
        15 * 1024 * 1024, ImagesOnly: false);

    /// <summary>A photographed bill: 2.5 MB is a phone photo, and nothing but an image will do.</summary>
    private static readonly AttachmentPolicy BillPhoto = new(
        [".png", ".jpg", ".jpeg", ".webp"], 2_621_440, ImagesOnly: true);

    public static AttachmentPolicy PolicyFor(string entityType) => entityType switch
    {
        ApprovalEntityTypes.ProjectExpense or ApprovalEntityTypes.SiteExpense => BillPhoto,
        _ => Default
    };

    public async Task<AttachmentDto> UploadAsync(string entityType, Guid entityId, string fileName, string contentType,
        Stream content, CancellationToken ct = default)
    {
        var policy = PolicyFor(entityType);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!policy.Extensions.Contains(ext))
            throw new AppException(policy.ImagesOnly
                ? $"'{ext}' is not a photo. Attach a JPG, PNG or WebP image of the bill."
                : $"File type '{ext}' is not allowed.", 400);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new AppException("File is empty.", 400);
        if (buffer.Length > policy.MaxBytes)
            throw new AppException($"That file is {buffer.Length / 1024m / 1024m:0.#} MB. "
                                 + $"The limit is {policy.Limit}.", 400);
        buffer.Position = 0;

        // The extension is what the uploader called the file; this is what the file actually is.
        // A renamed executable or a PDF saved as .jpg would otherwise be stored and handed back to
        // a browser later as an image.
        if (policy.ImagesOnly && !LooksLikeImage(buffer))
            throw new AppException("That file is not a JPG, PNG or WebP image, whatever it is named.", 400);
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

    /// <summary>The first bytes of the three formats a bill photo may be in.</summary>
    private static bool LooksLikeImage(MemoryStream buffer)
    {
        var head = new byte[12];
        buffer.Position = 0;
        var read = buffer.Read(head, 0, head.Length);
        if (read < 12) return false;

        // JPEG: FF D8 FF.  PNG: 89 'P' 'N' 'G'.  WebP: 'RIFF' .... 'WEBP'.
        if (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return true;
        if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47) return true;
        return head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
            && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50;
    }
}
