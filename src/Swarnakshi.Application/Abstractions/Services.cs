using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>Tenant of the signed-in user. Null for anonymous requests and for platform operators.</summary>
    Guid? CompanyId { get; }

    /// <summary>True when the caller is an EnterpriseAdmin — a platform operator, not a company user.</summary>
    bool IsPlatformAdmin { get; }

    string? Username { get; }
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
    /// <summary>Token for a company user. Carries the tenant id — the API trusts nothing else for scoping.</summary>
    TokenPair Issue(User user, Company company, IEnumerable<string> permissions);

    /// <summary>Token for a platform operator. Carries no tenant, so tenant endpoints reject it.</summary>
    TokenPair IssuePlatform(PlatformUser user);
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
