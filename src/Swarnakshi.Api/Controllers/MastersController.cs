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
public class MaterialsController(IMaterialService materials) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] Guid? categoryId,
        [FromQuery] Guid? subcategoryId, [FromQuery] string? brand, [FromQuery] Guid? unitId,
        [FromQuery] bool? active, CancellationToken ct)
        => this.Envelope(await materials.ListAsync(paging, categoryId, subcategoryId, brand, unitId, active, ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => this.Envelope(await materials.SummaryAsync(ct));

    [HttpGet("brands")]
    public async Task<IActionResult> Brands(CancellationToken ct)
        => this.Envelope(await materials.BrandsAsync(ct));

    /// <summary>Specification fields declared by a subcategory — drives the dynamic Add/Edit form.</summary>
    [HttpGet("spec-definitions")]
    public async Task<IActionResult> SpecDefinitions([FromQuery] Guid? subcategoryId, CancellationToken ct)
        => this.Envelope(await materials.SpecDefinitionsAsync(subcategoryId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await materials.GetAsync(id, ct));

    /// <summary>Stock by site, read from inventory — never stored on the material.</summary>
    [HttpGet("{id:guid}/stock")]
    public async Task<IActionResult> Stock(Guid id, CancellationToken ct)
        => this.Envelope(await materials.SiteStockAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Create(SaveMaterialRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await materials.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Update(Guid id, SaveMaterialRequest req, CancellationToken ct)
        => this.Envelope(await materials.UpdateAsync(id, req, ct));

    /// <summary>Lifecycle, not deletion — history and inventory rows are left untouched.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => this.Envelope(await materials.DeactivateAsync(id, ct), "Material deactivated.");

    [HttpPost("{id:guid}/reactivate")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => this.Envelope(await materials.ReactivateAsync(id, ct), "Material reactivated.");
}

/// <summary>
/// Contractor / customer / supplier master. Same shape, one controller.
/// Lifecycle is Active ↔ Inactive — there is deliberately no DELETE, because contracts, payments,
/// projects and purchases must keep resolving their party.
/// </summary>
[ApiController]
[Authorize]
public class PartiesController(IPartyService parties) : ControllerBase
{
    private const string PartyRoute = "^(contractors|customers|suppliers)$";

    private static PartyKind Kind(string route) => route switch
    {
        "contractors" => PartyKind.Contractor,
        "customers" => PartyKind.Customer,
        _ => PartyKind.Supplier
    };

    [HttpGet("api/{party:regex(" + PartyRoute + ")}")]
    public async Task<IActionResult> List(string party, [FromQuery] PageQuery paging,
        [FromQuery] bool? active, [FromQuery] string? type, CancellationToken ct)
        => this.Envelope(await parties.ListAsync(Kind(party), paging, active, type, ct));

    [HttpGet("api/{party:regex(" + PartyRoute + ")}/summary")]
    public async Task<IActionResult> Summary(string party, CancellationToken ct)
        => this.Envelope(await parties.SummaryAsync(Kind(party), ct));

    /// <summary>Distinct contractor types in use — populates the filter.</summary>
    [HttpGet("api/{party:regex(" + PartyRoute + ")}/types")]
    public async Task<IActionResult> Types(string party, CancellationToken ct)
        => this.Envelope(await parties.TypesAsync(Kind(party), ct));

    [HttpGet("api/{party:regex(" + PartyRoute + ")}/{id:guid}")]
    public async Task<IActionResult> Get(string party, Guid id, CancellationToken ct)
        => this.Envelope(await parties.GetAsync(Kind(party), id, ct));

    [HttpPost("api/{party:regex(" + PartyRoute + ")}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Create(string party, SavePartyRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await parties.CreateAsync(Kind(party), req, ct));

    [HttpPut("api/{party:regex(" + PartyRoute + ")}/{id:guid}")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Update(string party, Guid id, SavePartyRequest req, CancellationToken ct)
        => this.Envelope(await parties.UpdateAsync(Kind(party), id, req, ct));

    [HttpPost("api/{party:regex(" + PartyRoute + ")}/{id:guid}/deactivate")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Deactivate(string party, Guid id, CancellationToken ct)
        => this.Envelope(await parties.DeactivateAsync(Kind(party), id, ct), "Deactivated.");

    [HttpPost("api/{party:regex(" + PartyRoute + ")}/{id:guid}/reactivate")]
    [RequiresPermission(Permissions.MastersManage)]
    public async Task<IActionResult> Reactivate(string party, Guid id, CancellationToken ct)
        => this.Envelope(await parties.ReactivateAsync(Kind(party), id, ct), "Reactivated.");
}
