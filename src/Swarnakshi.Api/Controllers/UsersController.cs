using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Application.Users;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[RequiresPermission(Permissions.UsersManage)]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => this.Envelope(await users.ListAsync(ct));

    [HttpGet("permission-keys")]
    public IActionResult PermissionKeys() => this.Envelope(users.AllPermissionKeys());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => this.Envelope(await users.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await users.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest req, CancellationToken ct)
        => this.Envelope(await users.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/password")]
    public async Task<IActionResult> SetPassword(Guid id, SetPasswordRequest req, CancellationToken ct)
    {
        await users.SetPasswordAsync(id, req, ct);
        return this.Envelope<object?>(null, "Password updated.");
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, SetPermissionsRequest req, CancellationToken ct)
        => this.Envelope(await users.SetPermissionsAsync(id, req, ct));

    [HttpPut("{id:guid}/sites")]
    public async Task<IActionResult> SetSites(Guid id, SetSitesRequest req, CancellationToken ct)
        => this.Envelope(await users.SetSitesAsync(id, req, ct));
}
