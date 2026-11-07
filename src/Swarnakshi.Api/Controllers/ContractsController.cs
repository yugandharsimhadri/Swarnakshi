using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
[TenantOnly]
public class ContractsController(IContractWorkService contracts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? projectId,
        [FromQuery] Guid? contractorId, [FromQuery] ContractWorkStatus? status, CancellationToken ct)
        => this.Envelope(await contracts.ListAsync(paging, projectId, contractorId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.Envelope(await contracts.GetAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ContractManage)]
    public async Task<IActionResult> Create(SaveContractWorkRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await contracts.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.ContractManage)]
    public async Task<IActionResult> Update(Guid id, SaveContractWorkRequest req, CancellationToken ct)
        => this.Envelope(await contracts.UpdateAsync(id, req, ct));
}

[ApiController]
[Route("api/contractor-payments")]
[Authorize]
[TenantOnly]
public class ContractorPaymentsController(IContractorPaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? projectId,
        [FromQuery] Guid? contractorId, [FromQuery] TransactionStatus? status, CancellationToken ct)
        => this.Envelope(await payments.ListAsync(paging, projectId, contractorId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.Envelope(await payments.GetAsync(id, ct));

    [HttpGet("ledger/{contractorId:guid}")]
    public async Task<IActionResult> Ledger(Guid contractorId, CancellationToken ct)
        => this.Envelope(await payments.LedgerAsync(contractorId, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> Create(SaveContractorPaymentRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await payments.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await payments.SubmitAsync(id, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> Update(Guid id, SaveContractorPaymentRequest req, CancellationToken ct)
        => this.Envelope(await payments.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> Cancel(Guid id, ReasonRequest req, CancellationToken ct)
        => this.Envelope(await payments.CancelAsync(id, req.Reason, ct));

    [HttpPost("{id:guid}/change-request")]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> RequestChange(Guid id, ChangeContractorPaymentRequest req, CancellationToken ct)
        => this.Envelope(await payments.RequestChangeAsync(id, req.Payment, req.Reason, ct));

    [HttpPost("{id:guid}/delete-request")]
    [RequiresPermission(Permissions.ContractorPaymentCreate)]
    public async Task<IActionResult> RequestDelete(Guid id, ReasonRequest req, CancellationToken ct)
        => this.Envelope(await payments.RequestDeleteAsync(id, req.Reason, ct));

    [HttpGet("changes/{changeId:guid}")]
    public async Task<IActionResult> GetChange(Guid changeId, CancellationToken ct)
        => this.Envelope(await payments.GetChangeAsync(changeId, ct));
}
