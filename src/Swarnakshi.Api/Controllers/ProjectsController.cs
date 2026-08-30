using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery page, [FromQuery] Guid? siteId,
        [FromQuery] ProjectStatus? status, [FromQuery] Guid? customerId, CancellationToken ct)
        => this.Envelope(await projects.ListAsync(page, siteId, status, customerId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => this.Envelope(await projects.GetAsync(id, ct));

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> Summary(Guid id, CancellationToken ct)
        => this.Envelope(await projects.SummaryAsync(id, ct));

    [HttpPost]
    [RequiresPermission(Permissions.ProjectsManage)]
    public async Task<IActionResult> Create(SaveProjectRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await projects.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permissions.ProjectsManage)]
    public async Task<IActionResult> Update(Guid id, SaveProjectRequest req, CancellationToken ct)
        => this.Envelope(await projects.UpdateAsync(id, req, ct));
}
