using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Infrastructure.Persistence;

namespace Swarnakshi.Infrastructure.Services;

/// <summary>Allocates human-readable transaction numbers. Call inside the caller's DB transaction.</summary>
public sealed class TransactionSequenceService(AppDbContext db, IDateTimeProvider clock) : ITransactionSequenceService
{
    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var year = clock.Today.Year;
        var seq = await db.TransactionSequences
            .FirstOrDefaultAsync(s => s.Prefix == prefix && s.Year == year, ct);

        if (seq is null)
        {
            seq = new TransactionSequence { Prefix = prefix, Year = year, LastNumber = 0 };
            db.TransactionSequences.Add(seq);
        }

        seq.LastNumber++;
        await db.SaveChangesAsync(ct);
        return $"{prefix}-{year}-{seq.LastNumber:00000}";
    }
}
