using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Customers;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Contractor / Customer master data: lifecycle, code locking, duplicate codes, search and the
/// rule that an inactive party cannot be picked for a new transaction while history still resolves.
/// </summary>
public class PartyMasterTests
{
    private static SavePartyRequest Contractor(string code, string name = "Ravi Constructions",
        string? type = "Civil", string? mobile = "9000000001")
        => new(code, name, "Ravi & Co", mobile, "ravi@example.com", "Hyderabad",
            "ABCDE1234F", "29ABCDE1234F1Z5", "HDFC · 000111222", type, null);

    private static SavePartyRequest Customer(string code, string name = "Ramesh Kumar")
        => new(code, name, null, "9000000002", "ramesh@example.com", "Vijayawada",
            "ABCDE1234F", "29ABCDE1234F1Z5", null, null, null);

    // ---- creation & duplicates ------------------------------------------

    [Fact]
    public async Task Creates_a_contractor_active_by_default()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));

        c.Code.Should().Be("CON-001");
        c.CompanyName.Should().Be("Ravi & Co");
        c.Type.Should().Be("Civil");
        c.BankDetails.Should().Be("HDFC · 000111222");
        c.IsActive.Should().BeTrue();       // no IsActive field on the request — always Active
        c.CodeLocked.Should().BeFalse();
        c.Usage.Total.Should().Be(0);
    }

    [Fact]
    public async Task Creates_a_customer_active_by_default()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Customer, Customer("CUST-100"));

        c.Code.Should().Be("CUST-100");
        c.IsActive.Should().BeTrue();
        c.Gstin.Should().Be("29ABCDE1234F1Z5");
    }

    [Fact]
    public async Task Rejects_a_duplicate_contractor_code()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        var act = () => svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001", "Someone Else"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Rejects_a_duplicate_customer_code()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        await svc.CreateAsync(PartyKind.Customer, Customer("CUST-100"));
        var act = () => svc.CreateAsync(PartyKind.Customer, Customer("CUST-100", "Another Person"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Allows_two_contractors_to_share_a_name()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001", "Ravi Kumar"));
        var second = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-002", "Ravi Kumar"));

        second.Name.Should().Be("Ravi Kumar");   // names are not a uniqueness key
    }

    // ---- validation ------------------------------------------------------

    [Fact]
    public async Task Rejects_a_malformed_pan_gstin_or_email()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var badPan = () => svc.CreateAsync(PartyKind.Contractor,
            new SavePartyRequest("C1", "X", null, null, null, null, "NOTAPAN", null, null, null, null));
        var badGstin = () => svc.CreateAsync(PartyKind.Contractor,
            new SavePartyRequest("C2", "X", null, null, null, null, null, "123", null, null, null));
        var badEmail = () => svc.CreateAsync(PartyKind.Contractor,
            new SavePartyRequest("C3", "X", null, null, "not-an-email", null, null, null, null, null, null));

        await badPan.Should().ThrowAsync<FluentValidation.ValidationException>();
        await badGstin.Should().ThrowAsync<FluentValidation.ValidationException>();
        await badEmail.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Accepts_a_record_with_only_the_required_fields()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Customer,
            new SavePartyRequest("CUST-MIN", "Minimal", null, null, null, null, null, null, null, null, null));

        c.Mobile.Should().BeNull();
        c.IsActive.Should().BeTrue();
    }

    // ---- read & update ---------------------------------------------------

    [Fact]
    public async Task Gets_and_updates_a_contractor()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var created = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        var fetched = await svc.GetAsync(PartyKind.Contractor, created.Id);
        fetched.Name.Should().Be("Ravi Constructions");

        var updated = await svc.UpdateAsync(PartyKind.Contractor, created.Id,
            Contractor("CON-001", "Ravi Constructions Pvt Ltd", "Electrical", "9111111111"));

        updated.Name.Should().Be("Ravi Constructions Pvt Ltd");
        updated.Type.Should().Be("Electrical");
        updated.Mobile.Should().Be("9111111111");
        updated.IsActive.Should().BeTrue();      // update never changes status
    }

    // ---- lifecycle -------------------------------------------------------

    [Fact]
    public async Task Deactivates_and_reactivates_a_contractor()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));

        (await svc.DeactivateAsync(PartyKind.Contractor, c.Id)).IsActive.Should().BeFalse();
        (await svc.ReactivateAsync(PartyKind.Contractor, c.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivates_and_reactivates_a_customer()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Customer, Customer("CUST-100"));

        (await svc.DeactivateAsync(PartyKind.Customer, c.Id)).IsActive.Should().BeFalse();
        (await svc.ReactivateAsync(PartyKind.Customer, c.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_party_with_history_may_still_be_deactivated()
    {
        // Unlike Material there is no stock guard — deactivation is always allowed.
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IPartyService>();

        var (contractorId, _) = await SeedContractWorkAsync(sp, db);

        var off = await svc.DeactivateAsync(PartyKind.Contractor, contractorId);

        off.IsActive.Should().BeFalse();
        off.Usage.Contracts.Should().Be(1);
        (await db.ContractWorks.CountAsync(w => w.ContractorId == contractorId)).Should().Be(1);
    }

    // ---- inactive parties cannot be used for new transactions ------------

    [Fact]
    public async Task Inactive_contractor_cannot_be_used_for_a_new_contract()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var parties = sp.GetRequiredService<IPartyService>();
        var contracts = sp.GetRequiredService<IContractWorkService>();

        var (contractorId, projectId) = await SeedContractWorkAsync(sp, db);
        await parties.DeactivateAsync(PartyKind.Contractor, contractorId);

        var act = () => contracts.CreateAsync(new SaveContractWorkRequest(
            projectId, contractorId, "Plumbing", null, 50_000m, 50_000m, null, null, null,
            ContractWorkStatus.Planned));

        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Inactive_customer_cannot_be_attached_to_a_new_project()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var parties = sp.GetRequiredService<IPartyService>();
        var projects = sp.GetRequiredService<IProjectService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        var customer = await parties.CreateAsync(PartyKind.Customer, Customer("CUST-100"));
        await parties.DeactivateAsync(PartyKind.Customer, customer.Id);

        var act = () => projects.CreateAsync(new SaveProjectRequest(
            "P-NEW", "Villa 9", null, site.Id, customer.Id, null, null, null, null, null,
            1_000_000m, 2_000_000m, ProjectStatus.Planned, null));

        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Inactive_parties_are_filtered_out_of_selection_lists_but_stay_searchable()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        await svc.DeactivateAsync(PartyKind.Contractor, c.Id);

        var active = await svc.ListAsync(PartyKind.Contractor, new PageQuery { PageSize = 100 }, true, null);
        var inactive = await svc.ListAsync(PartyKind.Contractor, new PageQuery { PageSize = 100 }, false, null);
        var all = await svc.ListAsync(PartyKind.Contractor, new PageQuery { PageSize = 100 }, null, null);

        active.Items.Should().NotContain(x => x.Id == c.Id);
        inactive.Items.Should().Contain(x => x.Id == c.Id);
        all.Items.Should().Contain(x => x.Id == c.Id);
        (await svc.GetAsync(PartyKind.Contractor, c.Id)).Code.Should().Be("CON-001");
    }

    [Fact]
    public async Task Historical_references_still_resolve_after_deactivation()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var parties = sp.GetRequiredService<IPartyService>();
        var contracts = sp.GetRequiredService<IContractWorkService>();

        var (contractorId, projectId) = await SeedContractWorkAsync(sp, db);
        await parties.DeactivateAsync(PartyKind.Contractor, contractorId);

        var listed = await contracts.ListAsync(new PageQuery { PageSize = 50 }, projectId, contractorId, null);

        listed.Items.Should().ContainSingle();
        listed.Items[0].ContractorName.Should().NotBeNullOrEmpty();   // inactive master still joins
    }

    // ---- code locking ----------------------------------------------------

    [Fact]
    public async Task Code_is_editable_while_the_party_is_unused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-OLD"));
        var updated = await svc.UpdateAsync(PartyKind.Contractor, c.Id, Contractor("CON-NEW"));

        updated.Code.Should().Be("CON-NEW");
        updated.CodeLocked.Should().BeFalse();
    }

    [Fact]
    public async Task Contractor_code_locks_once_a_contract_references_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IPartyService>();

        var (contractorId, _) = await SeedContractWorkAsync(sp, db);

        (await svc.GetAsync(PartyKind.Contractor, contractorId)).CodeLocked.Should().BeTrue();

        var act = () => svc.UpdateAsync(PartyKind.Contractor, contractorId, Contractor("CON-CHANGED"));
        await act.Should().ThrowAsync<AppException>().WithMessage("*can no longer be changed*");
    }

    [Fact]
    public async Task Customer_code_locks_once_a_project_references_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var customer = await svc.CreateAsync(PartyKind.Customer, Customer("CUST-100"));
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        db.Sites.Add(site);
        db.Projects.Add(new Project
        {
            Code = "P1", Name = "Villa 1", Site = site, CustomerId = customer.Id, Status = ProjectStatus.Active
        });
        await db.SaveChangesAsync();

        (await svc.GetAsync(PartyKind.Customer, customer.Id)).CodeLocked.Should().BeTrue();

        var act = () => svc.UpdateAsync(PartyKind.Customer, customer.Id, Customer("CUST-CHANGED"));
        await act.Should().ThrowAsync<AppException>().WithMessage("*can no longer be changed*");
    }

    [Fact]
    public async Task Other_fields_stay_editable_after_the_code_locks()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var svc = sp.GetRequiredService<IPartyService>();

        var (contractorId, _) = await SeedContractWorkAsync(sp, db);
        var current = await svc.GetAsync(PartyKind.Contractor, contractorId);

        var updated = await svc.UpdateAsync(PartyKind.Contractor, contractorId,
            Contractor(current.Code, "Renamed Contractor", "Painting", "9222222222"));

        updated.Name.Should().Be("Renamed Contractor");
        updated.Type.Should().Be("Painting");
    }

    // ---- search & filter -------------------------------------------------

    [Fact]
    public async Task Search_is_case_insensitive_and_covers_code_name_company_mobile_and_type()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));

        foreach (var term in new[] { "con-001", "CON-001", "ravi", "RAVI & CO", "9000000001", "civil" })
        {
            var hits = await svc.ListAsync(PartyKind.Contractor, new PageQuery { Q = term }, null, null);
            hits.Items.Should().ContainSingle($"'{term}' should match the contractor");
        }
    }

    [Fact]
    public async Task Filters_by_contractor_type()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001", type: "Civil"));
        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-002", "Second", "Plumbing"));

        var civil = await svc.ListAsync(PartyKind.Contractor, new PageQuery { PageSize = 50 }, null, "Civil");
        var types = await svc.TypesAsync(PartyKind.Contractor);

        civil.Items.Should().ContainSingle(x => x.Code == "CON-001");
        types.Should().BeEquivalentTo(["Civil", "Plumbing"]);
    }

    [Fact]
    public async Task Summary_counts_active_and_inactive()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var a = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-002", "Second"));
        await svc.DeactivateAsync(PartyKind.Contractor, a.Id);

        var s = await svc.SummaryAsync(PartyKind.Contractor);

        s.Total.Should().Be(2);
        s.Active.Should().Be(1);
        s.Inactive.Should().Be(1);
    }

    // ---- audit -----------------------------------------------------------

    [Fact]
    public async Task Writes_audit_rows_for_the_whole_lifecycle()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        await svc.UpdateAsync(PartyKind.Contractor, c.Id, Contractor("CON-001", "Renamed"));
        await svc.DeactivateAsync(PartyKind.Contractor, c.Id);
        await svc.ReactivateAsync(PartyKind.Contractor, c.Id);

        var actions = await db.AuditLogs.Where(a => a.EntityType == "Contractor" && a.EntityId == c.Id)
            .Select(a => a.Action).ToListAsync();

        actions.Should().BeEquivalentTo(
            ["Contractor created", "Contractor updated", "Contractor deactivated", "Contractor reactivated"]);
    }

    [Fact]
    public async Task Writes_audit_rows_for_customers_too()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<IPartyService>();

        var c = await svc.CreateAsync(PartyKind.Customer, Customer("CUST-100"));
        await svc.DeactivateAsync(PartyKind.Customer, c.Id);

        var actions = await db.AuditLogs.Where(a => a.EntityType == "Customer" && a.EntityId == c.Id)
            .Select(a => a.Action).ToListAsync();

        actions.Should().BeEquivalentTo(["Customer created", "Customer deactivated"]);
    }

    // ---- permissions -----------------------------------------------------
    // Writes are gated by [RequiresPermission(masters.manage)] on PartiesController, so the
    // role -> permission map is what decides who may modify master data.

    [Fact]
    public void Owner_may_manage_contractors_and_customers()
        => Permissions.ForRole(UserRole.Owner).Should().Contain(Permissions.MastersManage);

    [Fact]
    public void Supervisor_cannot_modify_master_data()
        => Permissions.ForRole(UserRole.Supervisor).Should().NotContain(Permissions.MastersManage);

    [Fact]
    public void Accountant_cannot_modify_master_data()
        => Permissions.ForRole(UserRole.Accountant).Should().NotContain(Permissions.MastersManage);

    // ---- shared arrangement ---------------------------------------------

    /// <summary>Creates a real contract so the contractor genuinely carries history.</summary>
    private static async Task<(Guid ContractorId, Guid ProjectId)> SeedContractWorkAsync(
        IServiceProvider sp, AppDbContext db)
    {
        var parties = sp.GetRequiredService<IPartyService>();
        var contracts = sp.GetRequiredService<IContractWorkService>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
        db.AddRange(site, project);
        await db.SaveChangesAsync();

        var contractor = await parties.CreateAsync(PartyKind.Contractor, Contractor("CON-001"));
        await contracts.CreateAsync(new SaveContractWorkRequest(
            project.Id, contractor.Id, "Civil", "Foundation", 100_000m, 100_000m, null, null, null,
            ContractWorkStatus.Planned));

        return (contractor.Id, project.Id);
    }
}
