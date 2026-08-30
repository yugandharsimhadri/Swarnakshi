using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

public record DecideBody(string? Remarks, bool AllowOverride);

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalsController(IApprovalService approvals) : ControllerBase
{
    [HttpGet]
    [RequiresPermission(Permissions.ApprovalsDecide)]
    public async Task<IActionResult> List([FromQuery] PageQuery page, [FromQuery] string? type,
        [FromQuery] bool pendingOnly = true, CancellationToken ct = default)
        => this.Envelope(await approvals.ListAsync(page, type, pendingOnly, ct));

    [HttpGet("count")]
    [RequiresPermission(Permissions.ApprovalsDecide)]
    public async Task<IActionResult> Count(CancellationToken ct)
        => this.Envelope(new { pending = await approvals.PendingCountAsync(ct) });

    [HttpGet("{id:guid}/history")]
    [RequiresPermission(Permissions.ApprovalsDecide)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
        => this.Envelope(await approvals.HistoryAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    [RequiresPermission(Permissions.ApprovalsDecide)]
    public async Task<IActionResult> Approve(Guid id, DecideBody body, CancellationToken ct)
        => this.Envelope(await approvals.DecideAsync(id, new ApprovalDecision(true, body.Remarks, body.AllowOverride), ct));

    [HttpPost("{id:guid}/reject")]
    [RequiresPermission(Permissions.ApprovalsDecide)]
    public async Task<IActionResult> Reject(Guid id, DecideBody body, CancellationToken ct)
        => this.Envelope(await approvals.DecideAsync(id, new ApprovalDecision(false, body.Remarks, body.AllowOverride), ct));
}
