using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Common;

namespace Swarnakshi.Api.Common;

/// <summary>Converts exceptions into the uniform error envelope. No stack traces leave the process.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            await Write(ctx, 400, "Validation failed.", ex.Errors.Select(e => e.ErrorMessage).ToList());
        }
        catch (AppException ex)
        {
            await Write(ctx, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (DbUpdateConcurrencyException)
        {
            await Write(ctx, 409, "This record was changed by someone else. Reload and try again.", Array.Empty<string>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            await Write(ctx, 500, "An unexpected error occurred.", Array.Empty<string>());
        }
    }

    private static async Task Write(HttpContext ctx, int status, string message, IReadOnlyList<string> errors)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            success = false,
            message,
            data = (object?)null,
            errors
        });
    }
}
