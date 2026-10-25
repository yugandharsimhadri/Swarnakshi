using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Employees;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

/// <summary>
/// The staff register. Gated at the class, reads included: "who works here and what do they earn"
/// is the question being answered, and answering it only on the write path would leave the list
/// readable by anyone with a login.
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize]
[TenantOnly]
[RequiresPermission(Permissions.EmployeesManage)]
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

    // The payments against an employee are money, and are gated separately - see below.
    [HttpPost]
    public async Task<IActionResult> Create(SaveEmployeeRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await employees.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveEmployeeRequest req, CancellationToken ct)
        => this.Envelope(await employees.UpdateAsync(id, req, ct));
}

/// <summary>
/// Salary, advances and bonuses. Gated with the staff register rather than with labour.create:
/// these used to share a key with a villa’s day labour, which was fine while the two were held by
/// the same people, and stopped being fine the moment an Engineer was given labour.create to pay
/// the gang in front of him. Paying the payroll belongs with seeing it.
/// </summary>
[ApiController]
[Route("api/employee-payments")]
[Authorize]
[TenantOnly]
[RequiresPermission(Permissions.EmployeesManage)]
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

    [HttpPost]
    public async Task<IActionResult> Create(SaveEmployeePaymentRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await payments.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await payments.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => this.Envelope(await payments.CancelAsync(id, ct));
}
