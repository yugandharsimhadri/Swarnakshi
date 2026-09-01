using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Services;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16, KeySize = 32, Iterations = 100_000;
    private static readonly HashAlgorithmName Algo = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algo, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algo, key.Length);
        return CryptographicOperations.FixedTimeEquals(candidate, key);
    }
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Swarnakshi";
    public string Audience { get; set; } = "Swarnakshi";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

/// <summary>Claim names shared by the token issuer and the request-side <c>ICurrentUser</c>.</summary>
public static class SwarnakshiClaims
{
    /// <summary>Tenant the token is scoped to. Absent on a platform token — that absence IS the isolation.</summary>
    public const string CompanyId = "company_id";
    public const string CompanyCode = "company_code";
    public const string Username = "username";
    public const string Permission = "perm";

    /// <summary>"tenant" or "platform". Read by the authorization policies.</summary>
    public const string TokenKind = "token_kind";
    public const string TenantKind = "tenant";
    public const string PlatformKind = "platform";

    /// <summary>Issued-at, unix seconds. Compared with User.TokensValidFrom to revoke live tokens.</summary>
    public const string IssuedAt = "swk_iat";
}

public sealed class JwtTokenService(JwtOptions options, IDateTimeProvider clock) : IJwtTokenService
{
    public TokenPair Issue(User user, Company company, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(SwarnakshiClaims.TokenKind, SwarnakshiClaims.TenantKind),
            // The tenant is carried IN the token and nowhere else: no header, no route segment and
            // no request body can change which company a request reads, because only the signature
            // decides it.
            new(SwarnakshiClaims.CompanyId, company.Id.ToString()),
            new(SwarnakshiClaims.CompanyCode, company.Code),
            new(SwarnakshiClaims.Username, user.Username),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(SwarnakshiClaims.IssuedAt, clock.Now.ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        claims.AddRange(permissions.Select(p => new Claim(SwarnakshiClaims.Permission, p)));

        return Build(claims);
    }

    public TokenPair IssuePlatform(PlatformUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(SwarnakshiClaims.TokenKind, SwarnakshiClaims.PlatformKind),
            new(SwarnakshiClaims.Username, user.Username),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        return Build(claims);
    }

    private TokenPair Build(List<Claim> claims)
    {
        var now = clock.Now;
        var accessExp = now.AddMinutes(options.AccessTokenMinutes);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            expires: accessExp.UtcDateTime, signingCredentials: creds);

        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new TokenPair(access, accessExp, refresh, now.AddDays(options.RefreshTokenDays));
    }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
