using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Customers;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/customer-payments")]
[Authorize]
public class CustomerPaymentsController(ICustomerPaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery page, [FromQuery] Guid? projectId,
        [FromQuery] Guid? customerId, CancellationToken ct)
        => this.Envelope(await payments.ListAsync(page, projectId, customerId, ct));

    [HttpGet("ledger/{customerId:guid}")]
    public async Task<IActionResult> Ledger(Guid customerId, CancellationToken ct)
        => this.Envelope(await payments.LedgerAsync(customerId, ct));

    [HttpPost]
    [RequiresPermission(Permissions.CustomerPaymentCreate)]
    public async Task<IActionResult> Create(SaveCustomerPaymentRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await payments.CreateAsync(req, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.CustomerPaymentCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBody body, CancellationToken ct)
        => this.Envelope(await payments.CancelAsync(id, body.Reason, ct));
}
