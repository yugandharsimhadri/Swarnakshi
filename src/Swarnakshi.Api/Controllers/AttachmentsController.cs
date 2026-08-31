using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Attachments;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController(IAttachmentService attachments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
        => this.Envelope(await attachments.ListAsync(entityType, entityId, ct));

    [HttpPost]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] string entityType, [FromForm] Guid entityId,
        IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { success = false, message = "No file provided." });
        await using var stream = file.OpenReadStream();
        var dto = await attachments.UploadAsync(entityType, entityId, file.FileName,
            file.ContentType ?? "application/octet-stream", stream, ct);
        return this.EnvelopeCreated(dto);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (content, fileName, contentType) = await attachments.DownloadAsync(id, ct);
        return File(content, contentType, fileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await attachments.DeleteAsync(id, ct);
        return this.Envelope<object?>(null, "Deleted.");
    }
}
