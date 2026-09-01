using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/sites")]
[Authorize]
[TenantOnly]
public class SitesController(ISiteService sites) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery paging, [FromQuery] SiteStatus? status, CancellationToken ct)
        => this.Envelope(await sites.ListAsync(paging, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await sites.GetAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.SitesManage)]
    public async Task<IActionResult> Create(SaveSiteRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await sites.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.SitesManage)]
    public async Task<IActionResult> Update(Guid id, SaveSiteRequest req, CancellationToken ct)
        => this.Envelope(await sites.UpdateAsync(id, req, ct));
}
