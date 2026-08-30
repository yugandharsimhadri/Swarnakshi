namespace Swarnakshi.Application.Common;

/// <summary>Uniform operation result. Controllers map this to the API envelope.</summary>
public class Result
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public int StatusCode { get; init; } = 200;

    public static Result Ok(string? message = null) => new() { Success = true, Message = message };
    public static Result Fail(string message, int status = 400, IReadOnlyList<string>? errors = null)
        => new() { Success = false, Message = message, StatusCode = status, Errors = errors ?? Array.Empty<string>() };
}

public class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(T data, string? message = null)
        => new() { Success = true, Data = data, Message = message };
    public static new Result<T> Fail(string message, int status = 400, IReadOnlyList<string>? errors = null)
        => new() { Success = false, Message = message, StatusCode = status, Errors = errors ?? Array.Empty<string>() };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
}

public class PageQuery
{
    private const int MaxPageSize = 200;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Q { get; set; }
    public string? Sort { get; set; }

    public int Skip => (Math.Max(1, Page) - 1) * Take;
    public int Take => Math.Clamp(PageSize, 1, MaxPageSize);
}
