using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
        => this.Envelope(await auth.LoginAsync(req, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
        => this.Envelope(await auth.RefreshAsync(req, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(currentUser.UserId!.Value, ct);
        return this.Envelope<object?>(null, "Logged out.");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => this.Envelope(await auth.MeAsync(currentUser.UserId!.Value, ct));
}
