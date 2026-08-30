using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Balances([FromQuery] Guid siteId, [FromQuery] Guid? categoryId,
        [FromQuery] bool lowStock, [FromQuery] string? q, CancellationToken ct)
        => this.Envelope(await inventory.BalancesAsync(siteId, categoryId, lowStock, q, ct));

    [HttpGet("{siteId:guid}/{materialId:guid}")]
    public async Task<IActionResult> MaterialDetail(Guid siteId, Guid materialId, CancellationToken ct)
        => this.Envelope(await inventory.MaterialDetailAsync(siteId, materialId, ct));

    [HttpGet("transactions")]
    public async Task<IActionResult> Ledger([FromQuery] PageQuery page, [FromQuery] Guid? siteId,
        [FromQuery] Guid? materialId, [FromQuery] Guid? projectId, [FromQuery] InventoryTransactionType? type,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => this.Envelope(await inventory.LedgerAsync(page, siteId, materialId, projectId, type, from, to, ct));

    [HttpPost("opening-stock")]
    [RequiresPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> OpeningStock(OpeningStockRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await inventory.OpeningStockAsync(req, ct));

    [HttpPost("adjustments")]
    [RequiresPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> Adjustment(AdjustmentRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await inventory.AdjustmentAsync(req, ct));

    [HttpPost("returns")]
    [RequiresPermission(Permissions.InventoryAdjust)]
    public async Task<IActionResult> Return(ReturnRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await inventory.ReturnFromProjectAsync(req, ct));
}
