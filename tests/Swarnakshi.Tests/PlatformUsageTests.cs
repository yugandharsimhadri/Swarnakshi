using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Platform;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// What the operator sees about customers who registered and then did nothing.
///
/// <para>The screen exists because a licence list cannot tell a thriving customer from one who
/// signed up in March and never opened it again, and those need opposite phone calls. The tests
/// here are about that distinction — and about the line the screen must not cross: it reads how
/// many rows a company has and when it last wrote one, never what is in them.</para>
/// </summary>
public class PlatformUsageTests
{
    private static RegisterCompanyRequest Registration(string code, string name) =>
        new(name, code, "ravi", "Ravi@12345", "Ravi@12345", null, null);

    [Fact]
    public async Task A_company_that_registered_and_never_started_is_told_apart_from_one_that_is_working()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var usage = sp.GetRequiredService<IPlatformUsageService>();

        var quiet = await registration.RegisterAsync(Registration("quiet", "Quiet Builders"));
        var busy = await registration.RegisterAsync(Registration("busy", "Busy Builders"));

        // The quiet one registered a month ago and has done nothing since. Backdated, because the
        // difference between "new" and "never started" is time passing and nothing else.
        var quietCompany = await db.Companies.IgnoreQueryFilters().FirstAsync(c => c.Id == quiet.CompanyId);
        quietCompany.CreatedAt = DateTimeOffset.UtcNow.AddDays(-40);
        await db.SaveChangesAsync();

        // The busy one has a site and a villa, entered as its own owner inside its own tenancy.
        var owner = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == busy.CompanyId);
        host.CurrentUser.SetUser(owner.Id, busy.CompanyId, UserRole.Owner, Application.Security.Permissions.All);
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(
            new SaveSiteRequest("GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        await sp.GetRequiredService<IProjectService>().CreateAsync(
            new SaveProjectRequest("V1", "Villa 1", null, site.Id, null, null, null, null, null, null,
                1_000_000, null, ProjectStatus.Active, 0, null));

        host.CurrentUser.SetPlatformUser(Guid.NewGuid());
        var report = await usage.UsageAsync();

        var quietRow = report.Items.Single(i => i.Code == "quiet");
        var busyRow = report.Items.Single(i => i.Code == "busy");

        quietRow.Engagement.Should().Be(Engagement.NeverStarted);
        quietRow.Projects.Should().Be(0);
        quietRow.Records.Should().Be(0);

        busyRow.Engagement.Should().Be(Engagement.Active);
        busyRow.Projects.Should().Be(1);
        busyRow.Writes30.Should().BeGreaterThan(0, "the audit trail stamps every write with its company");
        busyRow.LastActivityAt.Should().NotBeNull();

        report.NeverStarted.Should().BeGreaterThanOrEqualTo(1);
        report.Active.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task A_company_registered_this_week_is_new_rather_than_a_failure()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var usage = sp.GetRequiredService<IPlatformUsageService>();

        await registration.RegisterAsync(Registration("fresh", "Fresh Builders"));

        // Registering seeds a company's master data, and that seeding writes audit rows. Those are
        // not a person working, so they are not counted - but a fortnight-old tenant with nothing in
        // it must still not read as a customer who has given up.
        var row = (await usage.UsageAsync()).Items.Single(i => i.Code == "fresh");

        row.Engagement.Should().NotBe(Engagement.NeverStarted);
        row.Engagement.Should().NotBe(Engagement.Dormant);
        row.DaysSinceRegistered.Should().BeLessThan(14);
    }

    [Fact]
    public async Task The_console_counts_the_work_a_company_did_not_the_catalogue_it_was_given()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var registration = sp.GetRequiredService<ICompanyRegistrationService>();
        var usage = sp.GetRequiredService<IPlatformUsageService>();

        var fresh = await registration.RegisterAsync(Registration("catalogue", "Catalogue Builders"));

        var row = (await usage.UsageAsync()).Items.Single(i => i.Id == fresh.CompanyId);

        // A new tenant is seeded with hundreds of units, materials and expense heads. Counting
        // those would make every empty company look busy and the screen worthless.
        row.Records.Should().Be(0);
        row.Sites.Should().Be(0);
        row.Projects.Should().Be(0);
        row.Users.Should().Be(1, "the company admin created at registration");
    }

    [Fact]
    public async Task The_report_carries_counts_and_dates_and_nothing_from_inside_a_row()
    {
        // The guarantee that lets an EnterpriseAdmin see across every tenant at all. If a figure
        // here ever became a name, an amount or a line of DataJson, this type would say so.
        var properties = typeof(CompanyUsageDto).GetProperties().Select(p => p.Name).ToList();

        properties.Should().BeEquivalentTo(new[]
        {
            "Id", "Code", "Name", "ContactEmail", "ContactMobile",
            "RegisteredAt", "DaysSinceRegistered",
            "LicenseExpiresOn", "DaysToExpiry", "IsExpired", "IsActive",
            "Users", "Sites", "Projects", "Records",
            "LastActivityAt", "DaysSinceActivity", "Writes7", "Writes30",
            "Engagement"
        }, "every field is a count, a date or the tenant's own identity - never its data");
    }
}
