using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

/// <summary>Read-only lookup lists used to populate dropdowns. Cached client-side.</summary>
[ApiController]
[Route("api")]
[Authorize]
public class LookupsController(IMasterService masters) : ControllerBase
{
    [HttpGet("units")]
    public async Task<IActionResult> Units(CancellationToken ct) => this.Envelope(await masters.UnitsAsync(ct));

    [HttpGet("material-categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) => this.Envelope(await masters.MaterialCategoriesAsync(ct));

    [HttpGet("material-subcategories")]
    public async Task<IActionResult> Subcategories([FromQuery] Guid? categoryId, CancellationToken ct)
        => this.Envelope(await masters.MaterialSubcategoriesAsync(categoryId, ct));

    [HttpGet("expense-heads")]
    public async Task<IActionResult> ExpenseHeads(CancellationToken ct) => this.Envelope(await masters.ExpenseHeadsAsync(ct));

    [HttpGet("expense-subheads")]
    public async Task<IActionResult> ExpenseSubheads([FromQuery] Guid? headId, CancellationToken ct)
        => this.Envelope(await masters.ExpenseSubheadsAsync(headId, ct));

    [HttpGet("labour-categories")]
    public async Task<IActionResult> LabourCategories(CancellationToken ct) => this.Envelope(await masters.LabourCategoriesAsync(ct));

    [HttpGet("payment-methods")]
    public async Task<IActionResult> PaymentMethods(CancellationToken ct) => this.Envelope(await masters.PaymentMethodsAsync(ct));

    [HttpGet("project-types")]
    public async Task<IActionResult> ProjectTypes(CancellationToken ct) => this.Envelope(await masters.ProjectTypesAsync(ct));
}

[ApiController]
[Route("api/materials")]
[Authorize]
public class MaterialsController(IMasterService masters) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery page, [FromQuery] Guid? categoryId, [FromQuery] bool? active, CancellationToken ct)
        => this.Envelope(await masters.MaterialsAsync(page, categoryId, active, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await masters.GetMaterialAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Create(SaveMaterialRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await masters.SaveMaterialAsync(null, req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Update(Guid id, SaveMaterialRequest req, CancellationToken ct)
        => this.Envelope(await masters.SaveMaterialAsync(id, req, ct));
}

/// <summary>Contractors / customers / suppliers — same shape, one controller.</summary>
[ApiController]
[Authorize]
public class PartiesController(IMasterService masters) : ControllerBase
{
    private static PartyKind Kind(string route) => route switch
    {
        "contractors" => PartyKind.Contractor,
        "customers" => PartyKind.Customer,
        _ => PartyKind.Supplier
    };

    [HttpGet("api/{party:regex(^(contractors|customers|suppliers)$)}")]
    public async Task<IActionResult> List(string party, [FromQuery] PageQuery page, [FromQuery] bool? active, CancellationToken ct)
        => this.Envelope(await masters.PartiesAsync(Kind(party), page, active, ct));

    [HttpPost("api/{party:regex(^(contractors|customers|suppliers)$)}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Create(string party, SavePartyRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await masters.SavePartyAsync(Kind(party), null, req, ct));

    [HttpPut("api/{party:regex(^(contractors|customers|suppliers)$)}/{id:guid}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Update(string party, Guid id, SavePartyRequest req, CancellationToken ct)
        => this.Envelope(await masters.SavePartyAsync(Kind(party), id, req, ct));
}
