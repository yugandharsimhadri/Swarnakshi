using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

public record ApprovalSettingsDto(decimal AutoApproveLimit);
public record SaveApprovalSettings(decimal AutoApproveLimit);

/// <summary>
/// Company-wide settings the Owner controls. Only the approval rule lives here so far, because it
/// is the only one that changes what other people in the company are allowed to do on their own.
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
[TenantOnly]
public class SettingsController(ISettingsService settings) : ControllerBase
{
    [HttpGet("approvals")]
    [RequiresPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> GetApprovals(CancellationToken ct)
        => this.Envelope(new ApprovalSettingsDto(await settings.AutoApproveLimitAsync(null, ct)));

    [HttpPut("approvals")]
    [RequiresPermission(Permissions.SettingsManage)]
    public async Task<IActionResult> SaveApprovals(SaveApprovalSettings body, CancellationToken ct)
    {
        if (body.AutoApproveLimit < 0)
            throw new AppException("The auto-approve limit cannot be negative. Use 0 to send everything for approval.", 400);
        // A limit large enough to cover every transaction the company will ever make is the same as
        // switching approvals off, and saying so plainly is better than letting someone type a row
        // of nines and discover months later that nothing was ever reviewed.
        if (body.AutoApproveLimit > 10_000_000m)
            throw new AppException("The auto-approve limit cannot exceed 1,00,00,000. A limit above that approves everything.", 400);

        await settings.SetAsync(SettingKeys.AutoApproveLimit,
            decimal.Round(body.AutoApproveLimit, 2).ToString(CultureInfo.InvariantCulture), null, ct);

        return this.Envelope(new ApprovalSettingsDto(await settings.AutoApproveLimitAsync(null, ct)));
    }
}
