using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Employees;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Codes are the app's business, not the user's.
///
/// A supervisor adding "Cement" from a phone should not have to invent MAT-CEM-OPC, check it is
/// free, and get it past a validator. These pin down that leaving a code out works, that the ones
/// minted are distinct, and — the part that would quietly corrupt data if it broke — that editing a
/// record without restating its code does not renumber it.
/// </summary>
public class AutoCodeTests
{
    private static SaveSiteRequest Site(string name, string? code = null)
        => new(code, name, null, null, null, null, null, null, SiteStatus.Active, null);

    private static SaveEmployeeRequest Employee(string name, string? code = null)
        => new(code, name, "9876543210", 25_000, new DateOnly(2026, 1, 1), null, null, null, null, null, true);

    [Fact]
    public async Task A_site_added_without_a_code_gets_one()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var created = await sites.CreateAsync(Site("Green Valley"));

        created.Code.Should().NotBeNullOrWhiteSpace();
        created.Code.Should().StartWith("SITE-");
    }

    [Fact]
    public async Task Two_sites_added_without_codes_do_not_collide()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var first = await sites.CreateAsync(Site("Green Valley"));
        var second = await sites.CreateAsync(Site("Sunrise"));

        second.Code.Should().NotBe(first.Code);
    }

    [Fact]
    public async Task A_code_typed_in_by_hand_is_still_honoured()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var created = await sites.CreateAsync(Site("Green Valley", "GV"));

        created.Code.Should().Be("GV", "an office that already numbers its sites should keep doing so");
    }

    [Fact]
    public async Task Editing_a_site_without_restating_its_code_keeps_the_code()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteService>();

        var created = await sites.CreateAsync(Site("Green Valley"));
        var renamed = await sites.UpdateAsync(created.Id, Site("Green Valley Phase 2"));

        renamed.Code.Should().Be(created.Code, "the edit screen no longer shows a code to resend");
        renamed.Name.Should().Be("Green Valley Phase 2");
    }

    [Fact]
    public async Task A_project_added_without_a_code_gets_one()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(Site("Green Valley"));
        var projects = sp.GetRequiredService<IProjectService>();

        var created = await projects.CreateAsync(new SaveProjectRequest(
            null, "Villa 101", "101", site.Id, null, null, null, null, null, null,
            5_000_000, null, ProjectStatus.Active, 0, null));

        created.Code.Should().StartWith("PRJ-");

        var edited = await projects.UpdateAsync(created.Id, new SaveProjectRequest(
            null, "Villa 101A", "101A", site.Id, null, null, null, null, null, null,
            5_000_000, null, ProjectStatus.Active, 10, null));
        edited.Code.Should().Be(created.Code);
    }

    [Fact]
    public async Task An_employee_added_without_a_code_gets_one()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        var created = await employees.CreateAsync(Employee("Suresh Kumar"));

        created.Code.Should().StartWith("EMP-");

        var edited = await employees.UpdateAsync(created.Id, Employee("Suresh Kumar Reddy"));
        edited.Code.Should().Be(created.Code);
    }

    [Fact]
    public async Task A_contractor_added_without_a_code_gets_one()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var parties = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var created = await parties.CreateAsync(PartyKind.Contractor,
            new SavePartyRequest(null, "Ramesh Plumbing", null, "9876543210", null, null, null, null, null, null, null));

        created.Code.Should().StartWith("CON-");

        var edited = await parties.UpdateAsync(PartyKind.Contractor, created.Id,
            new SavePartyRequest(null, "Ramesh Plumbing & Sanitary", null, "9876543210", null, null, null, null, null, null, null));
        edited.Code.Should().Be(created.Code, "renaming a contractor must not renumber them");
    }

    /// <summary>
    /// The material form asks for a name, a category and little else. Everything the old form
    /// demanded — code, unit, min stock, reorder level, rate, GST — has to be optional or the
    /// simplification is only skin deep.
    /// </summary>
    [Fact]
    public async Task A_material_needs_only_a_name_and_a_subcategory()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var materials = sp.GetRequiredService<IMaterialService>();

        var sub = await db.MaterialSubcategories.AsNoTracking().FirstAsync();

        var created = await materials.CreateAsync(new SaveMaterialRequest(
            Code: null, Name: "Pumice Block", MaterialSubcategoryId: sub.Id, Brand: null,
            UnitId: null, SecondaryUnitId: null, ConversionFactor: null, GenericMeasurement: null,
            MinStockLevel: 0, ReorderLevel: 0, DefaultPurchaseRate: 0, GstRate: null,
            Description: null, Notes: null, Specifications: null));

        created.Code.Should().StartWith("MAT-");
        created.UnitId.Should().NotBe(Guid.Empty, "stock still has to be counted in something");
        created.UnitCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Editing_a_material_without_a_code_keeps_the_one_it_has()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var materials = sp.GetRequiredService<IMaterialService>();
        var sub = await db.MaterialSubcategories.AsNoTracking().FirstAsync();

        SaveMaterialRequest Req(string name) => new(
            null, name, sub.Id, null, null, null, null, null, 0, 0, 0, null, null, null, null);

        var created = await materials.CreateAsync(Req("Pumice Block"));
        var edited = await materials.UpdateAsync(created.Id, Req("Pumice Block (washed)"));

        edited.Code.Should().Be(created.Code);
        edited.Name.Should().Be("Pumice Block (washed)");
    }
}
