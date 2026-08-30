using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool Has(string permissionKey);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public interface IJwtTokenService
{
    TokenPair Issue(User user, IEnumerable<string> permissions);
}

public interface ITransactionSequenceService
{
    /// <summary>Reserves and returns the next number, e.g. "PUR-2026-00001". Must run inside a DB transaction.</summary>
    Task<string> NextAsync(string prefix, CancellationToken ct = default);
}

public interface IDateTimeProvider
{
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<Stream> OpenAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
