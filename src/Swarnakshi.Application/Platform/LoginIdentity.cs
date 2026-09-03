using System.Text.RegularExpressions;

namespace Swarnakshi.Application.Platform;

/// <summary>
/// Parses and validates what a person types into the login box. Three shapes are accepted:
///
/// <list type="bullet">
///   <item><c>username@companycode</c> — a company user. Reads like an email on purpose, but the
///   right-hand side is the tenant, which is how the same "owner" exists in every company.</item>
///   <item>a mobile number — the same company user, taking the short way in. Their number already
///   picks out the company, so the <c>@companycode</c> is redundant.</item>
///   <item>a bare username, no digits — a platform operator (EnterpriseAdmin), which has no
///   company.</item>
/// </list>
/// </summary>
public static partial class LoginIdentity
{
    public const int MinCodeLength = 2;
    public const int MaxCodeLength = 30;
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 60;

    /// <summary>Indian mobile numbers are 10 digits; the canonical stored form drops any country code.</summary>
    public const int MobileLength = 10;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*[a-z0-9]$")] private static partial Regex CodePattern();
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")] private static partial Regex UsernamePattern();
    [GeneratedRegex(@"^[\d+(][\d\s\-()]*$")] private static partial Regex PhoneShapePattern();

    /// <summary>
    /// A parsed login. Exactly one of <see cref="Mobile"/> and (<see cref="Username"/> +
    /// <see cref="CompanyCode"/>) is set; a bare username has <see cref="Username"/> only.
    /// </summary>
    public readonly record struct Parsed(string? Username, string? CompanyCode, string? Mobile)
    {
        public bool IsMobile => Mobile is not null;

        /// <summary>A username with no company and no mobile — the EnterpriseAdmin.</summary>
        public bool IsPlatform => Mobile is null && CompanyCode is null;
    }

    /// <summary>
    /// Classifies the login box's contents. Returns false rather than throwing, because this runs
    /// on unauthenticated input. A value with an '@' is a company login; a value that is only
    /// phone characters is a mobile number; anything else is treated as a bare username.
    /// </summary>
    public static bool TryParse(string? login, out Parsed parsed)
    {
        parsed = default;
        var value = login?.Trim();
        if (string.IsNullOrEmpty(value)) return false;

        // '@' wins: an office that hands out "ravi@acme" should always get the company path, even
        // if the local part happens to be all digits.
        var at = value.LastIndexOf('@');
        if (at >= 0)
        {
            var username = value[..at].ToLowerInvariant();
            var code = value[(at + 1)..].ToLowerInvariant();
            if (!IsValidUsername(username) || !IsValidCompanyCode(code)) return false;
            parsed = new Parsed(username, code, null);
            return true;
        }

        if (LooksLikeMobile(value))
        {
            var mobile = NormaliseMobile(value);
            if (mobile is null) return false;
            parsed = new Parsed(null, null, mobile);
            return true;
        }

        var bare = value.ToLowerInvariant();
        if (!IsValidUsername(bare)) return false;
        parsed = new Parsed(bare, null, null);
        return true;
    }

    /// <summary>True if the value is made only of phone characters (digits, spaces, +, -, brackets).</summary>
    public static bool LooksLikeMobile(string? value)
        => !string.IsNullOrWhiteSpace(value) && PhoneShapePattern().IsMatch(value.Trim());

    /// <summary>
    /// Reduces a typed phone number to its canonical 10-digit form, dropping a country code (+91,
    /// 91) or a trunk zero. Returns null if what is left is not 10 digits — that is what makes it
    /// "not a valid mobile" for both login and the user form.
    /// </summary>
    public static string? NormaliseMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];

        return digits.Length == MobileLength ? digits : null;
    }

    public static bool IsValidMobile(string? raw) => NormaliseMobile(raw) is not null;

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
