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
}
