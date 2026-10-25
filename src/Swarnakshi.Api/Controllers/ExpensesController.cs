using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Abstractions;
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

    /// <summary>Sends an own-pocket claim, saved as a draft so the bill could be attached, to the Owner.</summary>
    [HttpPost("{id:guid}/submit")]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
        => this.Envelope(await expenses.SubmitAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequiresPermission(Permissions.ExpenseCreate)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBody body, CancellationToken ct)
        => this.Envelope(await expenses.CancelAsync(id, body.Reason, ct));
}

public record CancelBody(string Reason);

/// <summary>
/// Changes the customer has asked for on a villa: an extra point in the kitchen, a different floor,
/// a wall moved. A register, not a ledger — reading it is open to anyone who can see the villa, and
/// nothing here touches the villa's cost. What the change really costs arrives as ordinary expenses.
/// </summary>
[ApiController]
[Route("api/add-ons")]
[Authorize]
[TenantOnly]
public class AddOnsController(IAddOnRequestService addOns) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid projectId, [FromQuery] AddOnStatus? status,
        CancellationToken ct)
        => this.Envelope(await addOns.ListAsync(projectId, status, ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] Guid projectId, CancellationToken ct)
        => this.Envelope(await addOns.SummaryAsync(projectId, ct));

    [HttpPost]
    [RequiresPermission(Permissions.AddOnManage)]
    public async Task<IActionResult> Create(SaveAddOnRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await addOns.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.AddOnManage)]
    public async Task<IActionResult> Update(Guid id, SaveAddOnRequest req, CancellationToken ct)
        => this.Envelope(await addOns.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/status")]
    [RequiresPermission(Permissions.AddOnManage)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] AddOnStatusBody body, CancellationToken ct)
        => this.Envelope(await addOns.SetStatusAsync(id, body.Status, body.Remarks, ct));
}

public record AddOnStatusBody(AddOnStatus Status, string? Remarks);

/// <summary>
/// Money the company owes its own people, and the payments that clear it.
///
/// <para>Reading is open to anyone signed in, because an engineer asking "what am I owed?" is the
/// commonest question this screen answers and the service scopes a non-settler to their own
/// claims. Paying out needs <see cref="Permissions.ExpenseSettle"/>.</para>
/// </summary>
[ApiController]
[Route("api/reimbursements")]
[Authorize]
[TenantOnly]
public class ReimbursementsController(IExpenseSettlementService settlements, ICurrentUser currentUser)
    : ControllerBase
{
    private bool CanSettle => currentUser.Has(Permissions.ExpenseSettle);

    /// <summary>The claims themselves. Without expense.settle this answers only your own.</summary>
    [HttpGet("claims")]
    public async Task<IActionResult> Claims([FromQuery] ReimbursementStatus? status,
        [FromQuery] Guid? userId, CancellationToken ct)
        => this.Envelope(await settlements.ClaimsAsync(status, CanSettle ? userId : currentUser.UserId, ct));

    /// <summary>What is owed, per person. The office's view of the whole liability.</summary>
    [HttpGet("owed")]
    [RequiresPermission(Permissions.ExpenseSettle)]
    public async Task<IActionResult> Owed(CancellationToken ct) => this.Envelope(await settlements.OwedAsync(ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? paidToUserId,
        CancellationToken ct)
        => this.Envelope(await settlements.ListAsync(paging, CanSettle ? paidToUserId : currentUser.UserId, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ExpenseSettle)]
    public async Task<IActionResult> Create(SaveSettlementRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await settlements.CreateAsync(req, ct));

    [HttpPost("{id:guid}/void")]
    [RequiresPermission(Permissions.ExpenseSettle)]
    public async Task<IActionResult> Void(Guid id, [FromBody] CancelBody body, CancellationToken ct)
        => this.Envelope(await settlements.VoidAsync(id, body.Reason, ct));
}

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
