using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Sites;

public record SiteDto(Guid Id, string Code, string Name, string? City, string? State, string? Pin,
    Guid? SupervisorUserId, DateOnly? StartDate, SiteStatus Status, string? Notes,
    int ProjectCount, decimal InventoryValue);

/// <summary>Code is optional — leave it null and one is minted. See <see cref="ICodeGenerator"/>.</summary>
public record SaveSiteRequest(string? Code, string Name, string? Address, string? City, string? State,
    string? Pin, Guid? SupervisorUserId, DateOnly? StartDate, SiteStatus Status, string? Notes);

public class SaveSiteValidator : AbstractValidator<SaveSiteRequest>
{
    public SaveSiteValidator()
    {
        RuleFor(x => x.Code).MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Pin).Matches(@"^\d{6}$").When(x => !string.IsNullOrWhiteSpace(x.Pin));
    }
}

public interface ISiteService
{
    Task<PagedResult<SiteDto>> ListAsync(PageQuery page, SiteStatus? status, CancellationToken ct = default);
    Task<SiteDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SiteDto> CreateAsync(SaveSiteRequest req, CancellationToken ct = default);
    Task<SiteDto> UpdateAsync(Guid id, SaveSiteRequest req, CancellationToken ct = default);
}

public class SiteService(IAppDbContext db, IValidator<SaveSiteRequest> validator, ICodeGenerator codes) : ISiteService
{
    public async Task<PagedResult<SiteDto>> ListAsync(PageQuery page, SiteStatus? status, CancellationToken ct = default)
    {
        var q = db.Sites.AsNoTracking();
        if (status is not null) q = q.Where(s => s.Status == status);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(s => s.Name.Contains(page.Q) || s.Code.Contains(page.Q));
        return await q.OrderBy(s => s.Name).Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<SiteDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.Sites.AsNoTracking().Where(s => s.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Site", id);

    public async Task<SiteDto> CreateAsync(SaveSiteRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var code = await codes.ResolveAsync(req.Code, CodePrefixes.Site, ct);
        if (await db.Sites.AnyAsync(s => s.Code == code, ct))
            throw new AppException($"Site code '{code}' already exists.", 409);

        var site = new Site
        {
            Code = code, Name = req.Name.Trim(), Address = req.Address, City = req.City,
            State = req.State, Pin = req.Pin, SupervisorUserId = req.SupervisorUserId,
            StartDate = req.StartDate, Status = req.Status, Notes = req.Notes
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return await GetAsync(site.Id, ct);
    }

    public async Task<SiteDto> UpdateAsync(Guid id, SaveSiteRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == id, ct)
                   ?? throw new NotFoundException("Site", id);
        // An edit that omits the code keeps the one the site already has — the screens no longer show it.
        var code = string.IsNullOrWhiteSpace(req.Code) ? site.Code : req.Code.Trim();
        if (await db.Sites.AnyAsync(s => s.Code == code && s.Id != id, ct))
            throw new AppException($"Site code '{code}' already exists.", 409);

        site.Code = code; site.Name = req.Name.Trim(); site.Address = req.Address; site.City = req.City;
        site.State = req.State; site.Pin = req.Pin; site.SupervisorUserId = req.SupervisorUserId;
        site.StartDate = req.StartDate; site.Status = req.Status; site.Notes = req.Notes;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Site, SiteDto>> Projection =
        s => new SiteDto(s.Id, s.Code, s.Name, s.City, s.State, s.Pin, s.SupervisorUserId,
            s.StartDate, s.Status, s.Notes,
            s.Projects.Count,
            s.InventoryBalances.Sum(b => (decimal?)b.Value) ?? 0m);
}
