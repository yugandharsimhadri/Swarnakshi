using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Dashboard;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[TenantOnly]
[RequiresPermission(Permissions.DashboardView)]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.Envelope(await dashboard.GetAsync(ct));
}
