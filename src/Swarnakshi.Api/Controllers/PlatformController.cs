using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Platform;

namespace Swarnakshi.Api.Controllers;

/// <summary>Company sign-up. Public by necessity — this is how a new tenant comes into existence.</summary>
[ApiController]
[Route("api/register")]
[AllowAnonymous]
// Anonymous and it writes: without a limit, anyone who finds the URL can create tenants in bulk.
[EnableRateLimiting("auth")]
public class RegistrationController(ICompanyRegistrationService registration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterCompanyRequest req, CancellationToken ct)
        => this.EnvelopeCreated(await registration.RegisterAsync(req, ct));

    /// <summary>Lets the sign-up form say "taken" while typing rather than after submitting.</summary>
    [HttpGet("code-available")]
    public async Task<IActionResult> CodeAvailable([FromQuery] string code, CancellationToken ct)
        => this.Envelope(new { code = LoginIdentity.NormaliseCode(code), available = await registration.IsCodeAvailableAsync(code, ct) });
}

public record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

/// <summary>
/// The EnterpriseAdmin console: licences and company-admin passwords. Every action here is about a
/// tenant, never inside one — there is no route in this controller that returns business data.
/// </summary>
[ApiController]
[Route("api/platform")]
[Authorize]
[PlatformOnly]
public class PlatformController(IPlatformAdminService platform) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<IActionResult> Companies([FromQuery] string? q, CancellationToken ct)
        => this.Envelope(await platform.ListCompaniesAsync(q, ct));

    [HttpGet("companies/{id:guid}")]
    public async Task<IActionResult> Company(Guid id, CancellationToken ct)
        => this.Envelope(await platform.GetCompanyAsync(id, ct));

    [HttpPut("companies/{id:guid}/license")]
    public async Task<IActionResult> SetLicense(Guid id, SetLicenseExpiryRequest req, CancellationToken ct)
        => this.Envelope(await platform.SetLicenseExpiryAsync(id, req, ct));

    [HttpPost("companies/{id:guid}/license/extend")]
    public async Task<IActionResult> ExtendLicense(Guid id, ExtendLicenseRequest req, CancellationToken ct)
        => this.Envelope(await platform.ExtendLicenseAsync(id, req, ct));

    [HttpPut("companies/{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetCompanyActiveRequest req, CancellationToken ct)
        => this.Envelope(await platform.SetActiveAsync(id, req, ct));

    [HttpPost("companies/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetAdminPassword(Guid id, ResetCompanyPasswordRequest req, CancellationToken ct)
        => this.Envelope(await platform.ResetAdminPasswordAsync(id, req, ct), "Password reset. The old sessions are signed out.");

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangeOwnPassword(ChangeOwnPasswordRequest req, CancellationToken ct)
    {
        await platform.ChangeOwnPasswordAsync(req.CurrentPassword, req.NewPassword, req.ConfirmPassword, ct);
        return this.Envelope<object?>(null, "Password changed.");
    }
}
