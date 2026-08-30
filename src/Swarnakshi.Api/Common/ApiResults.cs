using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Application.Common;

namespace Swarnakshi.Api.Common;

/// <summary>The wire envelope every endpoint returns.</summary>
public record ApiEnvelope<T>(bool Success, string? Message, T? Data, IReadOnlyList<string> Errors)
{
    public static ApiEnvelope<T> Ok(T data, string? message = null) => new(true, message, data, Array.Empty<string>());
}

public static class ControllerExtensions
{
    public static IActionResult Envelope<T>(this ControllerBase c, T data, string? message = null)
        => c.Ok(new ApiEnvelope<T>(true, message, data, Array.Empty<string>()));

    public static IActionResult EnvelopeCreated<T>(this ControllerBase c, T data, string? message = null)
        => c.StatusCode(201, new ApiEnvelope<T>(true, message, data, Array.Empty<string>()));
}
