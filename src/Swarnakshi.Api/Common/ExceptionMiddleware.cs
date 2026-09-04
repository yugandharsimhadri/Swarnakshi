using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;

namespace Swarnakshi.Api.Common;

/// <summary>
/// Turns every exception into the uniform error envelope, and writes it to the log first.
///
/// <para>Two audiences, and they need opposite things. The caller gets a sentence they can act on
/// and no stack trace — an internal type name or a SQL fragment on screen is at best noise and at
/// worst a map of the system. The log gets everything: the exception, the path, who was signed in,
/// which tenant, and a reference.</para>
///
/// <para>The reference is what joins them. It goes in the response and in the log line, so someone
/// reporting "it said error 4f3a9c" can be answered by searching for that string rather than by
/// guessing which of the afternoon's entries was theirs.</para>
///
/// <para>Expected failures are logged too, at Warning rather than Error. "Insufficient stock" is
/// the product working correctly, but a run of them at one site is worth seeing, and a log that
/// only records crashes cannot show that.</para>
/// </summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx, ICurrentUser currentUser)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            var reference = Reference();
            logger.LogWarning("Validation failed [{Reference}] on {Method} {Path} for {User}: {Errors}",
                reference, ctx.Request.Method, ctx.Request.Path, Describe(currentUser),
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));

            await Write(ctx, 400, "Validation failed.", ex.Errors.Select(e => e.ErrorMessage).ToList(), reference);
        }
        catch (AppException ex)
        {
            // A rule the product enforces on purpose - not enough stock, already approved, wrong
            // site. Worth a line, not an alarm.
            var reference = Reference();
            logger.LogWarning(ex, "Refused [{Reference}] {Status} on {Method} {Path} for {User}: {Message}",
                reference, ex.StatusCode, ctx.Request.Method, ctx.Request.Path, Describe(currentUser), ex.Message);

            await Write(ctx, ex.StatusCode, ex.Message, ex.Errors, reference);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var reference = Reference();
            logger.LogWarning(ex, "Concurrent edit [{Reference}] on {Method} {Path} for {User}",
                reference, ctx.Request.Method, ctx.Request.Path, Describe(currentUser));

            await Write(ctx, 409, "This record was changed by someone else. Reload and try again.",
                Array.Empty<string>(), reference);
        }
        catch (Exception ex)
        {
            // Nobody predicted this one. Everything that might explain it goes in the log, and the
            // caller gets the reference and nothing else.
            var reference = Reference();
            logger.LogError(ex, "Unhandled [{Reference}] on {Method} {Path} for {User} from {ClientIp}",
                reference, ctx.Request.Method, ctx.Request.Path, Describe(currentUser),
                ctx.Connection.RemoteIpAddress?.ToString() ?? "-");

            await Write(ctx, 500, "Something went wrong. Quote this reference when reporting it.",
                Array.Empty<string>(), reference);
        }
    }

    /// <summary>
    /// Short enough to read down a phone and long enough not to collide within a day's logs. The
    /// trace id when there is one, so a request already correlated stays correlated.
    /// </summary>
    private static string Reference() =>
        System.Diagnostics.Activity.Current?.TraceId.ToString()[..8]
        ?? Guid.NewGuid().ToString("N")[..8];

    private static string Describe(ICurrentUser user) =>
        user.IsAuthenticated
            ? $"{user.Username}({(user.IsPlatformAdmin ? "platform" : user.CompanyId?.ToString() ?? "-")})"
            : "anonymous";

    private static async Task Write(HttpContext ctx, int status, string message,
        IReadOnlyList<string> errors, string reference)
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
            errors,
            reference,
        });
    }
}
