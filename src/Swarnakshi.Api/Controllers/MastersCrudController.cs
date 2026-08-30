using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

/// <summary>CRUD for the small name/active masters (units, categories, heads, labour categories, …).</summary>
[ApiController]
[Route("api/simple-masters")]
[Authorize]
[RequiresPermission(Permissions.MastersManage)]
public class SimpleMastersController(ISimpleMasterService svc) : ControllerBase
{
    [HttpPost("{kind}")]
    public async Task<IActionResult> Create(SimpleMasterKind kind, SaveSimpleMasterRequest req, CancellationToken ct)
        => this.EnvelopeCreated(new { id = await svc.SaveAsync(kind, null, req, ct) });

    [HttpPut("{kind}/{id:guid}")]
    public async Task<IActionResult> Update(SimpleMasterKind kind, Guid id, SaveSimpleMasterRequest req, CancellationToken ct)
        => this.Envelope(new { id = await svc.SaveAsync(kind, id, req, ct) });

    [HttpDelete("{kind}/{id:guid}")]
    public async Task<IActionResult> Delete(SimpleMasterKind kind, Guid id, CancellationToken ct)
    {
        await svc.DeleteAsync(kind, id, ct);
        return this.Envelope<object?>(null, "Deleted.");
    }
}
