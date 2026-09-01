using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Platform;

/// <summary>
/// Fills a brand-new company with the master data every builder needs on day one — units, the
/// material taxonomy, expense heads, labour categories, payment methods, settings. Implemented in
/// Infrastructure because the seed data and the seeders live with the DbContext.
/// </summary>
public interface ICompanyProvisioner
{
    Task ProvisionAsync(Guid companyId, CancellationToken ct = default);
}

public interface ICompanyRegistrationService
{
    Task<RegisterCompanyResponse> RegisterAsync(RegisterCompanyRequest request, CancellationToken ct = default);
    Task<bool> IsCodeAvailableAsync(string code, CancellationToken ct = default);
}

public class CompanyRegistrationService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ICompanyProvisioner provisioner,
    IDateTimeProvider clock,
    IRegistrationPolicy policy) : ICompanyRegistrationService
{
    public async Task<bool> IsCodeAvailableAsync(string code, CancellationToken ct = default)
    {
        var normalised = LoginIdentity.NormaliseCode(code);
        if (!LoginIdentity.IsValidCompanyCode(normalised)) return false;
        return !await db.Companies.AnyAsync(c => c.Code == normalised, ct);
    }

    public async Task<RegisterCompanyResponse> RegisterAsync(RegisterCompanyRequest request, CancellationToken ct = default)
    {
        var name = (request.CompanyName ?? "").Trim();
        var code = LoginIdentity.NormaliseCode(request.CompanyCode);
        var username = LoginIdentity.NormaliseUsername(request.Username);

        var errors = new List<string>();
        if (name.Length is < 2 or > 200) errors.Add("Company name must be between 2 and 200 characters.");
        if (!LoginIdentity.IsValidCompanyCode(code))
            errors.Add($"Company code must be {LoginIdentity.MinCodeLength}–{LoginIdentity.MaxCodeLength} characters, "
                       + "lowercase letters, digits or hyphens, and start and end with a letter or digit.");
        if (!LoginIdentity.IsValidUsername(username))
            errors.Add($"Username must be {LoginIdentity.MinUsernameLength}–{LoginIdentity.MaxUsernameLength} characters: "
                       + "lowercase letters, digits, dot, underscore or hyphen.");
        if ((request.Password ?? "").Length < 8) errors.Add("Password must be at least 8 characters.");
        if (request.Password != request.ConfirmPassword) errors.Add("The two passwords do not match.");
        if (errors.Count > 0) throw new AppException("Registration details are not valid.", 400, errors);

        // The company code is the login namespace, so a duplicate would make "owner@code" ambiguous.
        // The unique index is the real guard; this check exists to give a usable message first.
        if (await db.Companies.AnyAsync(c => c.Code == code, ct))
            throw new AppException($"The company code '{code}' is already taken. Please choose another.", 409);

        var company = new Company
        {
            Code = code,
            Name = name,
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            ContactMobile = string.IsNullOrWhiteSpace(request.ContactMobile) ? null : request.ContactMobile.Trim(),
            LicenseExpiresOn = clock.Today.AddDays(policy.TrialDays),
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        // Everything below belongs to the new tenant. Registration runs unauthenticated, so there
        // is no ambient company — the scope supplies it to both the filter and the insert stamp.
        using (db.BeginTenantScope(company.Id))
        {
            db.Users.Add(new User
            {
                Name = name,
                Username = username,
                Email = company.ContactEmail,
                PasswordHash = hasher.Hash(request.Password!),
                Role = UserRole.Owner,
                IsCompanyAdmin = true,
                IsActive = true
            });
            await db.SaveChangesAsync(ct);

            await provisioner.ProvisionAsync(company.Id, ct);
        }

        return new RegisterCompanyResponse(company.Id, company.Code, company.Name,
            LoginIdentity.Format(username, company.Code), company.LicenseExpiresOn);
    }
}

/// <summary>How long a newly registered company may use the product before an EnterpriseAdmin renews it.</summary>
public interface IRegistrationPolicy
{
    int TrialDays { get; }
}
