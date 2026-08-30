using Microsoft.EntityFrameworkCore;

namespace Swarnakshi.Application.Common;

public static class QueryExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query, PageQuery page, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);
        return new PagedResult<T>
        {
            Items = items, Page = Math.Max(1, page.Page), PageSize = page.Take, Total = total
        };
    }
}
