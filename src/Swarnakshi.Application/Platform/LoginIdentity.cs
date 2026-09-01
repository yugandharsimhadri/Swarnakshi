using System.Text.RegularExpressions;

namespace Swarnakshi.Application.Platform;

/// <summary>
/// Parses and validates what a person types into the login box.
///
/// A company user signs in as <c>username@companycode</c>. That reads like an email on purpose —
/// it is familiar — but it is not one: the right-hand side is the tenant, which is how the same
/// "owner" can exist in every company without collision. A platform operator (EnterpriseAdmin)
/// has no company, so it signs in with a bare username and no '@'.
/// </summary>
public static partial class LoginIdentity
{
    public const int MinCodeLength = 2;
    public const int MaxCodeLength = 30;
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 60;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*[a-z0-9]$")] private static partial Regex CodePattern();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")] private static partial Regex UsernamePattern();

    /// <summary>A parsed login. <paramref name="CompanyCode"/> is null for a platform operator.</summary>
    public readonly record struct Parsed(string Username, string? CompanyCode)
    {
        public bool IsPlatform => CompanyCode is null;
    }

    /// <summary>
    /// Splits at the LAST '@' so a username may itself contain one; returns false rather than
    /// throwing, because this runs on unauthenticated input.
    /// </summary>
    public static bool TryParse(string? login, out Parsed parsed)
    {
        parsed = default;
        var value = login?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value)) return false;

        var at = value.LastIndexOf('@');
        if (at < 0)
        {
            if (!IsValidUsername(value)) return false;
            parsed = new Parsed(value, null);
            return true;
        }

        var username = value[..at];
        var code = value[(at + 1)..];
        if (!IsValidUsername(username) || !IsValidCompanyCode(code)) return false;

        parsed = new Parsed(username, code);
        return true;
    }

    public static bool IsValidCompanyCode(string? code) =>
        code is not null
        && code.Length is >= MinCodeLength and <= MaxCodeLength
        && CodePattern().IsMatch(code);

    public static bool IsValidUsername(string? username) =>
        username is not null
        && username.Length is >= MinUsernameLength and <= MaxUsernameLength
        && UsernamePattern().IsMatch(username);

    /// <summary>Normalises a company code as typed into the canonical stored form.</summary>
    public static string NormaliseCode(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormaliseUsername(string? username) => (username ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>The login a company user types, rebuilt for display.</summary>
    public static string Format(string username, string companyCode) => $"{username}@{companyCode}";
}
