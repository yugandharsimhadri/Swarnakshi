using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Masters;

// ---- DTOs ----------------------------------------------------------------

/// <summary>Row shape for the Contractor / Customer / Supplier master lists.</summary>
public record PartyListDto(Guid Id, string Code, string Name, string? CompanyName, string? Mobile,
    string? Email, string? Gstin, string? Type, bool IsActive);

/// <summary>How many transactions point at this party. Read-only; drives the code lock and the detail view.</summary>
public record PartyUsageDto(int Contracts, int ContractorPayments, int Projects, int CustomerPayments, int Purchases)
{
    public int Total => Contracts + ContractorPayments + Projects + CustomerPayments + Purchases;
}

public record PartyDetailDto(Guid Id, string Code, string Name, string? CompanyName, string? Mobile,
    string? Email, string? Address, string? Pan, string? Gstin, string? BankDetails, string? Type,
    bool IsActive, string? Notes, bool CodeLocked, PartyUsageDto Usage);

public record PartySummaryDto(int Total, int Active, int Inactive);

/// <summary>Create/update payload. Status is deliberately absent — lifecycle runs through
/// deactivate/reactivate so it is audited and confirmed, never flipped by a silent field.</summary>
public record SavePartyRequest(string Code, string Name, string? CompanyName, string? Mobile, string? Email,
    string? Address, string? Pan, string? Gstin, string? BankDetails, string? Type, string? Notes);

public class SavePartyValidator : AbstractValidator<SavePartyRequest>
{
    // Kept deliberately lenient — these are optional fields the app has always treated as free text.
    private const string PanPattern = @"^[A-Za-z]{5}[0-9]{4}[A-Za-z]$";
    private const string GstinPattern = @"^[0-9]{2}[A-Za-z]{5}[0-9]{4}[A-Za-z][0-9A-Za-z]{3}$";
    private const string MobilePattern = @"^[+]?[0-9][0-9\s\-]{7,17}$";

    public SavePartyValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Mobile).Matches(MobilePattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Mobile))
            .WithMessage("Mobile must be 8-18 digits, optionally starting with '+'.");
        RuleFor(x => x.Pan).Matches(PanPattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Pan))
            .WithMessage("PAN must look like ABCDE1234F.");
        RuleFor(x => x.Gstin).Matches(GstinPattern)
            .When(x => !string.IsNullOrWhiteSpace(x.Gstin))
            .WithMessage("GSTIN must be 15 characters, e.g. 29ABCDE1234F1Z5.");
    }
}

// ---- Service -------------------------------------------------------------

public interface IPartyService
{
    Task<PagedResult<PartyListDto>> ListAsync(PartyKind kind, PageQuery page, bool? active, string? type,
        CancellationToken ct = default);
    Task<PartySummaryDto> SummaryAsync(PartyKind kind, CancellationToken ct = default);
    Task<IReadOnlyList<string>> TypesAsync(PartyKind kind, CancellationToken ct = default);
    Task<PartyDetailDto> GetAsync(PartyKind kind, Guid id, CancellationToken ct = default);
    Task<PartyDetailDto> CreateAsync(PartyKind kind, SavePartyRequest req, CancellationToken ct = default);
    Task<PartyDetailDto> UpdateAsync(PartyKind kind, Guid id, SavePartyRequest req, CancellationToken ct = default);
    Task<PartyDetailDto> DeactivateAsync(PartyKind kind, Guid id, CancellationToken ct = default);
    Task<PartyDetailDto> ReactivateAsync(PartyKind kind, Guid id, CancellationToken ct = default);
}

/// <summary>
/// Master-data management for contractors, customers and suppliers. One service, because all three
/// share the same shape and the same lifecycle — Active ↔ Inactive, never deleted, because
/// historical contracts, payments, projects and purchases must keep resolving.
/// </summary>
public class PartyService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<SavePartyRequest> validator) : IPartyService
{
    // ---- queries ---------------------------------------------------------

    public async Task<PagedResult<PartyListDto>> ListAsync(PartyKind kind, PageQuery page, bool? active,
        string? type, CancellationToken ct = default)
    {
        // Lowercased both sides: EF maps Contains to SQLite's case-sensitive instr().
        var term = string.IsNullOrWhiteSpace(page.Q) ? null : page.Q.Trim().ToLowerInvariant();
        var kindType = string.IsNullOrWhiteSpace(type) ? null : type;

        // Filter and order on the concrete entity, project last — EF cannot translate predicates
        // applied on top of a positional-record projection.
        IQueryable<PartyListDto> q = kind switch
        {
            PartyKind.Contractor => db.Contractors.AsNoTracking()
                .Where(c => active == null || c.IsActive == active)
                .Where(c => kindType == null || c.ContractorType == kindType)
                .Where(c => term == null
                    || c.Code.ToLower().Contains(term)
                    || c.Name.ToLower().Contains(term)
                    || (c.CompanyName != null && c.CompanyName.ToLower().Contains(term))
                    || (c.Mobile != null && c.Mobile.ToLower().Contains(term))
                    || (c.Email != null && c.Email.ToLower().Contains(term))
                    || (c.Gstin != null && c.Gstin.ToLower().Contains(term))
                    || (c.ContractorType != null && c.ContractorType.ToLower().Contains(term)))
                .OrderBy(c => c.Name).ThenBy(c => c.Code)
                .Select(c => new PartyListDto(c.Id, c.Code, c.Name, c.CompanyName, c.Mobile,
                    c.Email, c.Gstin, c.ContractorType, c.IsActive)),

            PartyKind.Customer => db.Customers.AsNoTracking()
                .Where(c => active == null || c.IsActive == active)
                .Where(c => term == null
                    || c.Code.ToLower().Contains(term)
                    || c.Name.ToLower().Contains(term)
                    || (c.Mobile != null && c.Mobile.ToLower().Contains(term))
                    || (c.Email != null && c.Email.ToLower().Contains(term))
                    || (c.Gstin != null && c.Gstin.ToLower().Contains(term)))
                .OrderBy(c => c.Name).ThenBy(c => c.Code)
                .Select(c => new PartyListDto(c.Id, c.Code, c.Name, null, c.Mobile,
                    c.Email, c.Gstin, null, c.IsActive)),

            _ => db.Suppliers.AsNoTracking()
                .Where(c => active == null || c.IsActive == active)
                .Where(c => term == null
                    || c.Code.ToLower().Contains(term)
                    || c.Name.ToLower().Contains(term)
                    || (c.Mobile != null && c.Mobile.ToLower().Contains(term))
                    || (c.Email != null && c.Email.ToLower().Contains(term))
                    || (c.Gstin != null && c.Gstin.ToLower().Contains(term)))
                .OrderBy(c => c.Name).ThenBy(c => c.Code)
                .Select(c => new PartyListDto(c.Id, c.Code, c.Name, null, c.Mobile,
                    c.Email, c.Gstin, null, c.IsActive)),
        };

        return await q.ToPagedAsync(page, ct);
    }

    public async Task<PartySummaryDto> SummaryAsync(PartyKind kind, CancellationToken ct = default)
    {
        var (total, active) = kind switch
        {
            PartyKind.Contractor => (await db.Contractors.CountAsync(ct),
                                     await db.Contractors.CountAsync(c => c.IsActive, ct)),
            PartyKind.Customer => (await db.Customers.CountAsync(ct),
                                   await db.Customers.CountAsync(c => c.IsActive, ct)),
            _ => (await db.Suppliers.CountAsync(ct), await db.Suppliers.CountAsync(c => c.IsActive, ct)),
        };
        return new PartySummaryDto(total, active, total - active);
    }

    /// <summary>Distinct contractor types actually in use — populates the filter without a new master table.</summary>
    public async Task<IReadOnlyList<string>> TypesAsync(PartyKind kind, CancellationToken ct = default)
        => kind != PartyKind.Contractor
            ? Array.Empty<string>()
            : await db.Contractors.AsNoTracking()
                .Where(c => c.ContractorType != null && c.ContractorType != "")
                .Select(c => c.ContractorType!).Distinct().OrderBy(t => t).ToListAsync(ct);

    public async Task<PartyDetailDto> GetAsync(PartyKind kind, Guid id, CancellationToken ct = default)
    {
        var usage = await UsageAsync(kind, id, ct);
        return kind switch
        {
            PartyKind.Contractor => Map(await db.Contractors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Contractor", id), usage),
            PartyKind.Customer => Map(await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Customer", id), usage),
            _ => Map(await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Supplier", id), usage),
        };
    }

    // ---- writes ----------------------------------------------------------

    public Task<PartyDetailDto> CreateAsync(PartyKind kind, SavePartyRequest req, CancellationToken ct = default)
        => SaveAsync(kind, null, req, ct);

    public Task<PartyDetailDto> UpdateAsync(PartyKind kind, Guid id, SavePartyRequest req, CancellationToken ct = default)
        => SaveAsync(kind, id, req, ct);

    private async Task<PartyDetailDto> SaveAsync(PartyKind kind, Guid? id, SavePartyRequest req, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(req, ct);

        var code = req.Code.Trim();
        var isNew = id is null;

        if (await CodeExistsAsync(kind, code, id ?? Guid.Empty, ct))
            throw new AppException($"{kind} code '{code}' already exists.", 409);

        Guid savedId;
        switch (kind)
        {
            case PartyKind.Contractor:
            {
                var e = isNew ? new Contractor() : await db.Contractors.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Contractor", id!);
                await GuardCodeAsync(kind, e.Id, e.Code, code, isNew, ct);
                e.Code = code; e.Name = req.Name.Trim(); e.CompanyName = Blank(req.CompanyName);
                e.Mobile = Blank(req.Mobile); e.Email = Blank(req.Email); e.Address = Blank(req.Address);
                e.Pan = Upper(req.Pan); e.Gstin = Upper(req.Gstin); e.BankDetails = Blank(req.BankDetails);
                e.ContractorType = Blank(req.Type); e.Notes = Blank(req.Notes);
                if (isNew) { e.IsActive = true; db.Contractors.Add(e); }
                savedId = e.Id;
                break;
            }
            case PartyKind.Customer:
            {
                var e = isNew ? new Customer() : await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Customer", id!);
                await GuardCodeAsync(kind, e.Id, e.Code, code, isNew, ct);
                e.Code = code; e.Name = req.Name.Trim(); e.Mobile = Blank(req.Mobile);
                e.Email = Blank(req.Email); e.Address = Blank(req.Address);
                e.Pan = Upper(req.Pan); e.Gstin = Upper(req.Gstin); e.Notes = Blank(req.Notes);
                if (isNew) { e.IsActive = true; db.Customers.Add(e); }
                savedId = e.Id;
                break;
            }
            default:
            {
                var e = isNew ? new Supplier() : await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Supplier", id!);
                await GuardCodeAsync(kind, e.Id, e.Code, code, isNew, ct);
                e.Code = code; e.Name = req.Name.Trim(); e.Mobile = Blank(req.Mobile);
                e.Email = Blank(req.Email); e.Address = Blank(req.Address);
                e.Pan = Upper(req.Pan); e.Gstin = Upper(req.Gstin); e.Notes = Blank(req.Notes);
                if (isNew) { e.IsActive = true; db.Suppliers.Add(e); }
                savedId = e.Id;
                break;
            }
        }

        Audit(kind, savedId, code, req.Name, isNew ? "created" : "updated");
        await db.SaveChangesAsync(ct);
        return await GetAsync(kind, savedId, ct);
    }

    public async Task<PartyDetailDto> DeactivateAsync(PartyKind kind, Guid id, CancellationToken ct = default)
        => await SetActiveAsync(kind, id, false, ct);

    public async Task<PartyDetailDto> ReactivateAsync(PartyKind kind, Guid id, CancellationToken ct = default)
        => await SetActiveAsync(kind, id, true, ct);

    /// <summary>
    /// Unlike Material there is no stock guard — a contractor or customer with history may be
    /// deactivated. Nothing about the historical rows is touched; the master simply stops appearing
    /// in new-transaction pickers (enforced in ContractService / ProjectService).
    /// </summary>
    private async Task<PartyDetailDto> SetActiveAsync(PartyKind kind, Guid id, bool active, CancellationToken ct)
    {
        string code, name;
        switch (kind)
        {
            case PartyKind.Contractor:
            {
                var e = await db.Contractors.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Contractor", id!);
                if (e.IsActive == active) return await GetAsync(kind, id, ct);
                e.IsActive = active; code = e.Code; name = e.Name;
                break;
            }
            case PartyKind.Customer:
            {
                var e = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Customer", id!);
                if (e.IsActive == active) return await GetAsync(kind, id, ct);
                e.IsActive = active; code = e.Code; name = e.Name;
                break;
            }
            default:
            {
                var e = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Supplier", id!);
                if (e.IsActive == active) return await GetAsync(kind, id, ct);
                e.IsActive = active; code = e.Code; name = e.Name;
                break;
            }
        }

        Audit(kind, id, code, name, active ? "reactivated" : "deactivated");
        await db.SaveChangesAsync(ct);
        return await GetAsync(kind, id, ct);
    }

    // ---- helpers ---------------------------------------------------------

    /// <summary>Queries the entity sets directly — EF cannot translate Any() over the projected list shape.</summary>
    private async Task<bool> CodeExistsAsync(PartyKind kind, string code, Guid excludeId, CancellationToken ct)
        => kind switch
        {
            PartyKind.Contractor => await db.Contractors.AnyAsync(x => x.Code == code && x.Id != excludeId, ct),
            PartyKind.Customer => await db.Customers.AnyAsync(x => x.Code == code && x.Id != excludeId, ct),
            _ => await db.Suppliers.AnyAsync(x => x.Code == code && x.Id != excludeId, ct),
        };

    private async Task<PartyUsageDto> UsageAsync(PartyKind kind, Guid id, CancellationToken ct) => kind switch
    {
        PartyKind.Contractor => new PartyUsageDto(
            await db.ContractWorks.CountAsync(x => x.ContractorId == id, ct),
            await db.ContractorPayments.CountAsync(x => x.ContractorId == id, ct), 0, 0, 0),
        PartyKind.Customer => new PartyUsageDto(0, 0,
            await db.Projects.CountAsync(x => x.CustomerId == id, ct),
            await db.CustomerPayments.CountAsync(x => x.CustomerId == id, ct), 0),
        _ => new PartyUsageDto(0, 0, 0, 0,
            await db.PurchaseHeaders.CountAsync(x => x.SupplierId == id, ct)),
    };

    /// <summary>Code is immutable once any transaction references the party, so history stays unambiguous.</summary>
    private async Task GuardCodeAsync(PartyKind kind, Guid id, string currentCode, string newCode,
        bool isNew, CancellationToken ct)
    {
        if (isNew || string.Equals(currentCode, newCode, StringComparison.Ordinal)) return;
        if ((await UsageAsync(kind, id, ct)).Total > 0)
            throw new AppException(
                $"This {kind.ToString().ToLowerInvariant()} has transaction history, so its code can no longer be changed.",
                409);
    }

    /// <summary>Contractor/Customer/Supplier are plain masters, so audit rows are written explicitly
    /// rather than by the AuditableEntity hook in SaveChangesAsync.</summary>
    private void Audit(PartyKind kind, Guid id, string code, string name, string action) =>
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = kind.ToString(),
            EntityId = id,
            Action = $"{kind} {action}",
            DataJson = $"{code} · {name}",
            UserId = currentUser.UserId,
            At = DateTimeOffset.UtcNow
        });

    private static PartyDetailDto Map(Contractor c, PartyUsageDto usage) => new(
        c.Id, c.Code, c.Name, c.CompanyName, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin,
        c.BankDetails, c.ContractorType, c.IsActive, c.Notes, usage.Total > 0, usage);

    private static PartyDetailDto Map(Customer c, PartyUsageDto usage) => new(
        c.Id, c.Code, c.Name, null, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin,
        null, null, c.IsActive, c.Notes, usage.Total > 0, usage);

    private static PartyDetailDto Map(Supplier c, PartyUsageDto usage) => new(
        c.Id, c.Code, c.Name, null, c.Mobile, c.Email, c.Address, c.Pan, c.Gstin,
        null, null, c.IsActive, c.Notes, usage.Total > 0, usage);

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Upper(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
}
