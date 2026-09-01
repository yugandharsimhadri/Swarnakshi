using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
[TenantOnly]
public class PurchasesController(IPurchaseService purchases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? siteId,
        [FromQuery] TransactionStatus? status, CancellationToken ct)
        => this.Envelope(await purchases.ListAsync(paging, siteId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await purchases.GetAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.PurchaseCreate)]
    public async Task<IActionResult> Create(SavePurchaseRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await purchases.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.PurchaseCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await purchases.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/payments")]
    [RequiresPermission(Permissions.PurchaseCreate)]
    public async Task<IActionResult> AddPayment(Guid id, SupplierPaymentInput input, CancellationToken ct)
        => this.Envelope(await purchases.AddPaymentAsync(id, input, ct));
}

[ApiController]
[Route("api/material-requests")]
[Authorize]
[TenantOnly]
public class MaterialRequestsController(IMaterialRequestService requests) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? projectId,
        [FromQuery] Guid? siteId, [FromQuery] MaterialRequestStatus? status, CancellationToken ct)
        => this.Envelope(await requests.ListAsync(paging, projectId, siteId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await requests.GetAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.MaterialRequestCreate)]
    public async Task<IActionResult> Create(SaveMaterialRequestRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await requests.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.MaterialRequestCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await requests.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/issue")]
    [RequiresPermission(Permissions.MaterialRequestCreate)]
    public async Task<IActionResult> Issue(Guid id, IssueRequest req, CancellationToken ct)
        => this.Envelope(await requests.IssueAsync(id, req, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.MaterialRequestCreate)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => this.Envelope(await requests.CancelAsync(id, ct));
}
