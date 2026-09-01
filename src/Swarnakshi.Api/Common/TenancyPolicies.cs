using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Services;

namespace Swarnakshi.Api.Common;

/// <summary>
/// A company endpoint. Rejects a platform token outright: an EnterpriseAdmin manages licences and
/// passwords, and is not a user of any company — so it must not be able to read one's business data.
/// Also refuses a tenant whose licence has lapsed or whose account is suspended.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TenantOnlyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var user = services.GetRequiredService<ICurrentUser>();

        if (!user.IsAuthenticated)
        {
            context.Result = Envelope(401, "Authentication required.");
            return;
        }

        if (user.IsPlatformAdmin)
        {
            context.Result = Envelope(403,
                "An EnterpriseAdmin account manages licences and passwords only, and cannot open a company's data.");
            return;
        }

        if (user.CompanyId is not { } companyId)
        {
            context.Result = Envelope(403, "This token is not scoped to a company.");
            return;
        }

        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<IDateTimeProvider>();
        var ct = context.HttpContext.RequestAborted;

        // A token issued before the account's last password reset is dead, even though its
        // signature and expiry are still good. Without this an admin reset would take an hour to
        // bite, which is exactly the hour a compromised session would use.
        var account = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == user.UserId)
            .Select(u => new { u.IsActive, u.TokensValidFrom })
            .FirstOrDefaultAsync(ct);

        if (account is null || !account.IsActive)
        {
            context.Result = Envelope(401, "This account is no longer active.");
            return;
        }

        if (account.TokensValidFrom is { } validFrom
            && long.TryParse(context.HttpContext.User.FindFirstValue(SwarnakshiClaims.IssuedAt), out var issuedAt)
            && DateTimeOffset.FromUnixTimeSeconds(issuedAt) < validFrom)
        {
            context.Result = Envelope(401, "Your password was changed. Please sign in again.");
            return;
        }

        var company = await db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Name, c.IsActive, c.LicenseExpiresOn })
            .FirstOrDefaultAsync(ct);

        if (company is null)
        {
            context.Result = Envelope(403, "This company no longer exists.");
            return;
        }

        if (!company.IsActive)
        {
            context.Result = Envelope(403, "This company account is suspended.");
            return;
        }

        // Checked per request, not only at sign-in: an access token outlives the moment it was
        // issued, so a licence that lapses mid-session has to stop working without waiting for it.
        if (company.LicenseExpiresOn < clock.Today)
        {
            context.Result = Envelope(402,
                $"The licence for {company.Name} expired on {company.LicenseExpiresOn:dd MMM yyyy}. "
                + "Ask your Swarnakshi administrator to renew it.");
        }
    }

    internal static ObjectResult Envelope(int status, string message) =>
        new(new { success = false, message, data = (object?)null, errors = Array.Empty<string>() })
        { StatusCode = status };
}

/// <summary>A platform endpoint: EnterpriseAdmin only. A company token is refused.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PlatformOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        if (!user.IsAuthenticated)
        {
            context.Result = TenantOnlyAttribute.Envelope(401, "Authentication required.");
            return;
        }

        if (!user.IsPlatformAdmin)
            context.Result = TenantOnlyAttribute.Envelope(403, "This area is for platform administrators only.");
    }
}
