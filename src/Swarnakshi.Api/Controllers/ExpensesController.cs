using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
[TenantOnly]
public class ExpensesController(IProjectExpenseService expenses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? projectId,
        [FromQuery] Guid? expenseHeadId, [FromQuery] ProjectExpenseType? type,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => this.Envelope(await expenses.ListAsync(paging, projectId, expenseHeadId, type, from, to, ct));

    [HttpGet("cost-by-head")]
    public async Task<IActionResult> CostByHead([FromQuery] Guid projectId, CancellationToken ct)
        => this.Envelope(await expenses.CostByHeadAsync(projectId, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Create(SaveProjectExpenseRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await expenses.CreateAsync(req, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBody body, CancellationToken ct)
        => this.Envelope(await expenses.CancelAsync(id, body.Reason, ct));
}

public record CancelBody(string Reason);

/// <summary>
/// Costs that belong to a site rather than to one villa — the watchman, temporary power, the site
/// office. Kept apart from project expenses so a villa's cost stays exactly what was spent on it.
/// </summary>
[ApiController]
[Route("api/site-expenses")]
[Authorize]
[TenantOnly]
public class SiteExpensesController(ISiteExpenseService expenses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? siteId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
        => this.Envelope(await expenses.ListAsync(paging, siteId, from, to, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Create(SaveSiteExpenseRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await expenses.CreateAsync(req, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBody body, CancellationToken ct)
        => this.Envelope(await expenses.CancelAsync(id, body.Reason, ct));
}

[ApiController]
[Route("api/labour")]
[Authorize]
[TenantOnly]
public class LabourController(ILabourService labour) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? projectId,
        [FromQuery] TransactionStatus? status, CancellationToken ct)
        => this.Envelope(await labour.ListAsync(paging, projectId, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.Envelope(await labour.GetAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Create(SaveLabourEntryRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await labour.CreateAsync(req, ct));

    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) => this.Envelope(await labour.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.LabourCreate)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => this.Envelope(await labour.CancelAsync(id, ct));
}
