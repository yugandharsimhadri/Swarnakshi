using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Platform;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Tenant isolation and the licence gate. These are the tests that make the SaaS split safe to
/// change: they assert that one company cannot see another's data no matter which service is asked.
/// </summary>
public class MultiTenancyTests
{
    private static RegisterCompanyRequest Registration(string code, string name = "Acme Builders") =>
        new(name, code, "ravi", "Ravi@12345", "Ravi@12345", null, null);

    // ---- registration ----------------------------------------------------

    [Fact]
    public async Task Registering_creates_a_company_its_admin_and_a_full_master_catalogue()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var db = sp.GetRequiredService<AppDbContext>();

        var res = await registration.RegisterAsync(Registration("acme"));

        res.CompanyCode.Should().Be("acme");
        res.Login.Should().Be("ravi@acme", "the login is username@companycode");

        using var acme = db.BeginTenantScope(res.CompanyId);
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Materials.CountAsync()).Should().BeGreaterThan(30, "a new tenant gets its own catalogue");
        (await db.MaterialCategories.CountAsync()).Should().Be(9);
        (await db.MaterialSubcategories.CountAsync()).Should().BeGreaterThan(180, "the detail moved down a level");
        (await db.ExpenseHeads.CountAsync()).Should().BeGreaterThan(20);
        (await db.Units.CountAsync()).Should().BeGreaterThan(15);
    }

    [Fact]
    public async Task Company_code_must_be_unique_but_the_name_need_not_be()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var registration = scope.ServiceProvider.GetRequiredService<ICompanyRegistrationService>();

        await registration.RegisterAsync(Registration("shared", "Shared Name"));

        var sameCode = () => registration.RegisterAsync(Registration("shared", "Different Name"));
        await sameCode.Should().ThrowAsync<AppException>().WithMessage("*already taken*");

        // Two real builders may legitimately trade under the same name.
        var sameName = await registration.RegisterAsync(Registration("other", "Shared Name"));
        sameName.CompanyName.Should().Be("Shared Name");
    }

    [Theory]
    [InlineData("a", "too short")]
    [InlineData("Has Spaces", "spaces are not allowed")]
    [InlineData("-leading", "cannot start with a hyphen")]
    [InlineData("has@at", "'@' would break the login format")]
    public async Task Invalid_company_codes_are_refused(string code, string why)
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var registration = scope.ServiceProvider.GetRequiredService<ICompanyRegistrationService>();

        var act = () => registration.RegisterAsync(Registration(code));

        (await act.Should().ThrowAsync<AppException>(why)).And.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task A_code_typed_in_capitals_is_accepted_and_normalised()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var registration = scope.ServiceProvider.GetRequiredService<ICompanyRegistrationService>();

        // Being strict here would only punish someone whose keyboard was in caps lock.
        var res = await registration.RegisterAsync(Registration("ACME"));

        res.CompanyCode.Should().Be("acme");
        res.Login.Should().Be("ravi@acme");
    }

    [Fact]
    public async Task The_two_passwords_must_match()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var registration = scope.ServiceProvider.GetRequiredService<ICompanyRegistrationService>();

        var act = () => registration.RegisterAsync(
            new RegisterCompanyRequest("Acme", "acme", "ravi", "Password1", "Password2", null, null));

        (await act.Should().ThrowAsync<AppException>()).And.Errors.Should().Contain(e => e.Contains("do not match"));
    }

    // ---- isolation -------------------------------------------------------

    [Fact]
    public async Task One_company_cannot_see_another_companys_sites_or_projects()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var db = sp.GetRequiredService<AppDbContext>();

        // The founding tenant builds something.
        var mine = await sites.CreateAsync(new SaveSiteRequest("S1", "My Site", null, null, null, null, null, null, SiteStatus.Active, null));
        await projects.CreateAsync(new SaveProjectRequest("P1", "My Villa", null, mine.Id, null, null, null, null, null, null, 100_000, null, ProjectStatus.Active, 0, null));

        // A second company registers and signs in.
        var acme = await registration.RegisterAsync(Registration("acme"));
        var acmeOwner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == acme.CompanyId);
        host.CurrentUser.SetUser(acmeOwner.Id, acme.CompanyId, UserRole.Owner, Swarnakshi.Application.Security.Permissions.All, "ravi");

        (await sites.ListAsync(new PageQuery { PageSize = 100 }, null)).Total
            .Should().Be(0, "a new company starts with no sites — and certainly not somebody else's");
        (await projects.ListAsync(new PageQuery { PageSize = 100 }, null, null, null)).Total
            .Should().Be(0);
    }

    [Fact]
    public async Task A_direct_id_lookup_across_tenants_returns_not_found_rather_than_the_row()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var sites = sp.GetRequiredService<ISiteService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var db = sp.GetRequiredService<AppDbContext>();

        var mine = await sites.CreateAsync(new SaveSiteRequest("S1", "My Site", null, null, null, null, null, null, SiteStatus.Active, null));

        var acme = await registration.RegisterAsync(Registration("acme"));
        var acmeOwner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == acme.CompanyId);
        host.CurrentUser.SetUser(acmeOwner.Id, acme.CompanyId, UserRole.Owner, Swarnakshi.Application.Security.Permissions.All, "ravi");

        // Guessing an id must not work: the filter applies to a keyed read exactly as to a list.
        var act = () => sites.GetAsync(mine.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task The_same_username_and_the_same_codes_may_exist_in_two_companies()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var sites = sp.GetRequiredService<ISiteService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var db = sp.GetRequiredService<AppDbContext>();

        await sites.CreateAsync(new SaveSiteRequest("GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));

        var acme = await registration.RegisterAsync(Registration("acme"));
        var acmeOwner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == acme.CompanyId);
        host.CurrentUser.SetUser(acmeOwner.Id, acme.CompanyId, UserRole.Owner, Swarnakshi.Application.Security.Permissions.All, "ravi");

        // Same site code, different tenant — the unique index is (CompanyId, Code), not (Code).
        var act = () => sites.CreateAsync(new SaveSiteRequest("GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Transaction_numbers_restart_per_company()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var sequences = sp.GetRequiredService<Swarnakshi.Application.Abstractions.ITransactionSequenceService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var db = sp.GetRequiredService<AppDbContext>();

        var first = await sequences.NextAsync("PUR");
        var second = await sequences.NextAsync("PUR");
        first.Should().EndWith("00001");
        second.Should().EndWith("00002");

        var acme = await registration.RegisterAsync(Registration("acme"));
        var acmeOwner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == acme.CompanyId);
        host.CurrentUser.SetUser(acmeOwner.Id, acme.CompanyId, UserRole.Owner, Swarnakshi.Application.Security.Permissions.All, "ravi");

        // Each company numbers its own documents from 1 — a shared counter would leak how much
        // business another tenant is doing.
        (await sequences.NextAsync("PUR")).Should().EndWith("00001");
    }

    [Fact]
    public async Task A_write_with_no_tenant_in_scope_fails_loudly_instead_of_writing_an_orphan()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        host.CurrentUser.SetPlatformUser(Guid.NewGuid());
        using var noTenant = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var freshDb = noTenant.ServiceProvider.GetRequiredService<AppDbContext>();

        freshDb.Sites.Add(new Domain.Entities.Site { Code = "X", Name = "Orphan", Status = SiteStatus.Active });

        var act = () => freshDb.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no tenant is in scope*");
    }

    // ---- login -----------------------------------------------------------

    [Fact]
    public async Task Sign_in_needs_the_company_code_and_the_wrong_one_is_refused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var auth = sp.GetRequiredService<IAuthService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();

        await registration.RegisterAsync(Registration("acme"));

        (await auth.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"))).Kind
            .Should().Be(AuthResponse.TenantKind);

        // Right password, wrong tenant.
        var wrongCompany = () => auth.LoginAsync(new LoginRequest("ravi@swarnakshi", "Ravi@12345"));
        await wrongCompany.Should().ThrowAsync<AppException>().WithMessage("Invalid username or password.");

        // An unknown company reports the same thing — the endpoint cannot be used to enumerate tenants.
        var unknownCompany = () => auth.LoginAsync(new LoginRequest("ravi@nosuchcompany", "Ravi@12345"));
        await unknownCompany.Should().ThrowAsync<AppException>().WithMessage("Invalid username or password.");
    }

    // ---- licence ---------------------------------------------------------

    [Fact]
    public async Task An_expired_licence_refuses_sign_in_and_a_renewal_restores_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var auth = sp.GetRequiredService<IAuthService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();

        var acme = await registration.RegisterAsync(Registration("acme"));

        await platform.SetLicenseExpiryAsync(acme.CompanyId,
            new SetLicenseExpiryRequest(new DateOnly(2020, 1, 1), "expired for the test"));

        var blocked = () => auth.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"));
        (await blocked.Should().ThrowAsync<AppException>().WithMessage("*expired*")).And.StatusCode.Should().Be(402);

        await platform.ExtendLicenseAsync(acme.CompanyId, new ExtendLicenseRequest(365));
        await auth.Invoking(a => a.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"))).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Renewing_a_lapsed_licence_counts_from_today_not_from_the_lapsed_date()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();
        var clock = sp.GetRequiredService<Swarnakshi.Application.Abstractions.IDateTimeProvider>();

        var acme = await registration.RegisterAsync(Registration("acme"));
        await platform.SetLicenseExpiryAsync(acme.CompanyId, new SetLicenseExpiryRequest(clock.Today.AddDays(-100), null));

        var renewed = await platform.ExtendLicenseAsync(acme.CompanyId, new ExtendLicenseRequest(30));

        renewed.LicenseExpiresOn.Should().Be(clock.Today.AddDays(30),
            "a renewal must not silently spend part of the period on days the tenant was locked out");
    }

    [Fact]
    public async Task A_suspended_company_cannot_sign_in()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var auth = sp.GetRequiredService<IAuthService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();

        var acme = await registration.RegisterAsync(Registration("acme"));
        await platform.SetActiveAsync(acme.CompanyId, new SetCompanyActiveRequest(false));

        var act = () => auth.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"));
        (await act.Should().ThrowAsync<AppException>().WithMessage("*suspended*")).And.StatusCode.Should().Be(403);
    }

    // ---- the EnterpriseAdmin --------------------------------------------

    [Fact]
    public async Task EnterpriseAdmin_signs_in_with_a_bare_username_and_holds_no_company_permissions()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var res = await auth.LoginAsync(new LoginRequest("EnterpriseAdmin", "SivAyAAn@HMS"));

        res.Kind.Should().Be(AuthResponse.PlatformKind);
        res.PlatformUser!.Username.Should().Be("enterpriseadmin");
        res.User.Should().BeNull("a platform operator is not a user of any company");
        res.Company.Should().BeNull();
    }

    [Fact]
    public async Task EnterpriseAdmin_resets_a_company_admin_password_and_kills_the_old_sessions()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var auth = sp.GetRequiredService<IAuthService>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();

        var acme = await registration.RegisterAsync(Registration("acme"));
        var session = await auth.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"));

        var overview = await platform.GetCompanyAsync(acme.CompanyId);
        var admin = overview.Admins.Should().ContainSingle().Subject;
        admin.Login.Should().Be("ravi@acme");

        await platform.ResetAdminPasswordAsync(acme.CompanyId,
            new ResetCompanyPasswordRequest(admin.UserId, "Fresh@12345", "Fresh@12345"));

        await auth.Invoking(a => a.LoginAsync(new LoginRequest("ravi@acme", "Fresh@12345"))).Should().NotThrowAsync();
        await auth.Invoking(a => a.LoginAsync(new LoginRequest("ravi@acme", "Ravi@12345"))).Should().ThrowAsync<AppException>();

        // The refresh token from before the reset is dead too.
        var reuse = () => auth.RefreshAsync(new RefreshRequest(session.RefreshToken));
        await reuse.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task A_mismatched_reset_password_is_refused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();

        var acme = await registration.RegisterAsync(Registration("acme"));
        var admin = (await platform.GetCompanyAsync(acme.CompanyId)).Admins[0];

        var act = () => platform.ResetAdminPasswordAsync(acme.CompanyId,
            new ResetCompanyPasswordRequest(admin.UserId, "Password1", "Password2"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*do not match*");
    }

    [Fact]
    public async Task The_console_lists_every_company_with_its_licence_state()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var platform = sp.GetRequiredService<IPlatformAdminService>();

        await registration.RegisterAsync(Registration("acme", "Acme Builders"));
        await registration.RegisterAsync(Registration("bharat", "Bharat Constructions"));

        var all = await platform.ListCompaniesAsync(null);
        all.Should().HaveCount(3, "the founding tenant plus the two that registered");
        all.Should().OnlyContain(c => c.DaysToExpiry > 0 && !c.IsExpired);

        var filtered = await platform.ListCompaniesAsync("acme");
        filtered.Should().ContainSingle().Which.Code.Should().Be("acme");
    }
}
