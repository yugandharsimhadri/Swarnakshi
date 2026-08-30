using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Swarnakshi.Application.Abstractions;

namespace Swarnakshi.Api.Common;

/// <summary>Backend permission gate. Use on controllers/actions in addition to [Authorize].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresPermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!user.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false, message = "Authentication required.", data = (object?)null, errors = Array.Empty<string>()
            });
            return;
        }
        if (!user.Has(permission))
        {
            context.Result = new ObjectResult(new
            {
                success = false, message = "You do not have permission to perform this action.",
                data = (object?)null, errors = Array.Empty<string>()
            })
            { StatusCode = 403 };
        }
    }
}
