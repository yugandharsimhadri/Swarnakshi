using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Auth;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Accepts both audiences — <c>username@companycode</c> or a bare platform username.</summary>
    /// <remarks>Rate limited: anonymous, reachable from the internet, and it tells you when a guess
    /// was right. The limit is per caller address, so one person fumbling a password does not lock
    /// out their colleagues.</remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
        => this.Envelope(await auth.LoginAsync(req, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
        => this.Envelope(await auth.RefreshAsync(req, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(ct);
        return this.Envelope<object?>(null, "Logged out.");
    }

    /// <summary>Who am I — and, for a company user, which tenant and how long the licence has left.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => this.Envelope(await auth.MeAsync(ct));
}
