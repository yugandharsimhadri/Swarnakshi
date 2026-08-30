namespace Swarnakshi.Application.Common;

/// <summary>Thrown for expected business-rule violations; mapped to a clean 4xx by the API middleware.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public IReadOnlyList<string> Errors { get; }

    public AppException(string message, int statusCode = 400, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? Array.Empty<string>();
    }
}

public sealed class NotFoundException(string entity, object key)
    : AppException($"{entity} '{key}' was not found.", 404);

public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message, 403);
