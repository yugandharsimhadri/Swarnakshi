using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Common;

/// <summary>
/// Allocates the short codes that identify masters — sites, projects, materials, people.
///
/// These used to be typed in. On a phone, by a site supervisor, that is a keyboard, a decision and a
/// duplicate-code error before any real work gets recorded. Nobody outside the office ever quotes a
/// material code, so the app now invents one and stops showing it on the entry screens.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>Next code for the prefix, e.g. "PRJ-0007". Runs in the caller's transaction if there is one.</summary>
    Task<string> NextAsync(string prefix, CancellationToken ct = default);

    /// <summary>The caller's code if they gave one, otherwise a fresh generated one.</summary>
    Task<string> ResolveAsync(string? supplied, string prefix, CancellationToken ct = default);
}

/// <summary>The prefixes master codes are minted under. One per master, stable forever.</summary>
public static class CodePrefixes
{
    public const string Site = "SITE";
    public const string Project = "PRJ";
    public const string Material = "MAT";
    public const string Employee = "EMP";
    public const string Contractor = "CON";
    public const string Customer = "CUS";
    public const string Supplier = "SUP";
}

/// <summary>
/// Shares the <see cref="TransactionSequence"/> table with transaction numbering, but under
/// <c>Year = 0</c> — a master code that restarted every January would collide with last year's.
/// </summary>
public sealed class CodeGenerator(IAppDbContext db) : ICodeGenerator
{
    private const int NotYearScoped = 0;

    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var seq = await db.TransactionSequences
            .FirstOrDefaultAsync(s => s.Prefix == prefix && s.Year == NotYearScoped, ct);

        if (seq is null)
        {
            seq = new TransactionSequence { Prefix = prefix, Year = NotYearScoped, LastNumber = 0 };
            db.TransactionSequences.Add(seq);
        }

        seq.LastNumber++;
        await db.SaveChangesAsync(ct);
        return $"{prefix}-{seq.LastNumber:0000}";
    }

    public async Task<string> ResolveAsync(string? supplied, string prefix, CancellationToken ct = default)
        => string.IsNullOrWhiteSpace(supplied) ? await NextAsync(prefix, ct) : supplied.Trim();
}
