using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Employees;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
[TenantOnly]
public class EmployeesController(IEmployeeService employees, IEmployeePaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] bool? active,
        [FromQuery] Guid? siteId, CancellationToken ct)
        => this.Envelope(await employees.ListAsync(paging, active, siteId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await employees.GetAsync(id, ct));

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> Ledger(Guid id, CancellationToken ct)
        => this.Envelope(await payments.LedgerAsync(id, ct));

    // The employee record is master data; the payments against it are money, and are gated separately.
    [HttpPost]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Create(SaveEmployeeRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await employees.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Update(Guid id, SaveEmployeeRequest req, CancellationToken ct)
        => this.Envelope(await employees.UpdateAsync(id, req, ct));
}

[ApiController]
[Route("api/employee-payments")]
[Authorize]
[TenantOnly]
public class EmployeePaymentsController(IEmployeePaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? employeeId,
        [FromQuery] Guid? projectId, [FromQuery] EmployeePaymentKind? kind,
        [FromQuery] TransactionStatus? status, CancellationToken ct)
        => this.Envelope(await payments.ListAsync(paging, employeeId, projectId, kind, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await payments.GetAsync(id, ct));

    // Salary and advances are labour cost, so they reuse the labour permission the Accountant holds.
    [HttpPost]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Create(SaveEmployeePaymentRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await payments.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await payments.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => this.Envelope(await payments.CancelAsync(id, ct));
}
