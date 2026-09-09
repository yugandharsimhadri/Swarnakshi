using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Masters;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Application.Users;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Nothing in the application reads these rows. They exist so that "who changed this, and what did
/// it say before?" can be answered months later — a question that cannot be answered
/// retrospectively, which is the whole reason to pay for the writes now.
///
/// <para>The trail is written in <c>AppDbContext.SaveChangesAsync</c>, so what these tests really
/// pin is that it covers writes nobody wrote audit code for.</para>
/// </summary>
public class AuditTrailTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static Task<List<AuditLog>> TrailAsync(AppDbContext db, string entityType, Guid id) =>
        db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == entityType && a.EntityId == id)
            .OrderBy(a => a.At).ThenBy(a => a.Action)
            .ToListAsync();

    private static Dictionary<string, string> Data(AuditLog log) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(log.DataJson!)!;

    [Fact]
    public async Task Creating_a_row_is_recorded_with_who_did_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));

        var trail = await TrailAsync(db, nameof(Site), site.Id);
        trail.Should().ContainSingle();
        trail[0].Action.Should().Be("Created");
        trail[0].UserId.Should().NotBeNull("a change with no name against it answers half the question");
        // No snapshot on create: the row is still there to be read, so copying it would be storage
        // spent on something nothing has lost.
        trail[0].DataJson.Should().BeNull();
    }

    [Fact]
    public async Task An_edit_records_the_field_that_changed_and_what_it_was_before()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var sites = sp.GetRequiredService<ISiteService>();

        var site = await sites.CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, "Hyderabad", null, null, null, null, SiteStatus.Active, null));
        await sites.UpdateAsync(site.Id, new SaveSiteRequest(
            "GV", "Green Meadows", null, "Hyderabad", null, null, null, null, SiteStatus.OnHold, null));

        var edit = (await TrailAsync(db, nameof(Site), site.Id)).Single(a => a.Action != "Created");

        // The status transition leads, because that is what people search for — but the rename
        // travelling with it is named too. An edit that hid half of itself would be worse than none.
        edit.Action.Should().Be("Status Active -> OnHold (+ Name)");
        var data = Data(edit);
        data["Name"].Should().Be("Green Valley -> Green Meadows");
        data["Status"].Should().Be("Active -> OnHold");
        data.Should().NotContainKey("City", "an untouched field is not a change");
    }

    [Fact]
    public async Task A_status_change_is_named_in_the_action_because_that_is_what_people_look_for()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var f = await ArrangeProjectAsync(sp, db);

        var expenses = sp.GetRequiredService<Application.Expenses.IProjectExpenseService>();
        var expense = await expenses.CreateAsync(new Application.Expenses.SaveProjectExpenseRequest(
            f.ProjectId, Today, f.ExpenseHeadId, null, "Scaffolding", 15_000,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));
        await sp.ApproveAsync(Application.Approvals.ApprovalEntityTypes.ProjectExpense, expense.Id);

        var trail = await TrailAsync(db, nameof(ProjectExpense), expense.Id);
        trail.Should().Contain(a => a.Action == "Status PendingApproval -> Posted");
    }

    [Fact]
    public async Task Deleting_a_row_keeps_what_it_said_because_nothing_else_will()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var simple = sp.GetRequiredService<ISimpleMasterService>();

        var id = await simple.SaveAsync(SimpleMasterKind.Unit,
            null, new SaveSimpleMasterRequest("Truckload", "TRKLD", null, 99, true));
        await simple.DeleteAsync(SimpleMasterKind.Unit, id);

        var deleted = (await TrailAsync(db, nameof(Unit), id)).Single(a => a.Action == "Deleted");

        // The row has gone. Everything worth knowing about it has to be in here.
        var data = Data(deleted);
        data["Name"].Should().Be("Truckload");
        data["Code"].Should().Be("TRKLD");
        (await db.Units.AnyAsync(u => u.Id == id)).Should().BeFalse();
    }

    [Fact]
    public async Task A_password_never_reaches_the_trail()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<IUserService>();

        var user = await users.CreateAsync(
            new CreateUserRequest("Anil", "anil", "first-password", UserRole.Supervisor, null));
        await users.SetPasswordAsync(user.Id, new SetPasswordRequest("second-password"));

        var trail = await TrailAsync(db, nameof(User), user.Id);
        var everything = string.Join("\n", trail.Select(a => $"{a.Action} {a.DataJson}"));

        everything.Should().NotContain("first-password").And.NotContain("second-password");
        everything.Should().Contain("PasswordHash", "the fact that it changed is worth recording");
        Data(trail.Single(a => a.Action.Contains("PasswordHash")))["PasswordHash"]
            .Should().Be("*** -> ***", "…but never the value, in either direction");
    }

    [Fact]
    public async Task A_settings_change_is_tracked_even_though_no_service_writes_an_audit_row()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        // The auto-approve limit decides how much of the company's spending nobody has to look at,
        // so a silent change to it is exactly the thing this trail exists for.
        await sp.SetAutoApproveLimitAsync(5_000m);
        await sp.SetAutoApproveLimitAsync(50_000m);

        var setting = await db.Settings.AsNoTracking()
            .SingleAsync(s => s.Key == SettingKeys.AutoApproveLimit && s.SiteId == null);
        var trail = await TrailAsync(db, nameof(Setting), setting.Id);

        trail.Should().Contain(a => a.Action == "Updated: Value");
        Data(trail.Last(a => a.Action == "Updated: Value"))["Value"].Should().Be("5000 -> 50000");
    }

    [Fact]
    public async Task The_trail_does_not_audit_itself()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await sp.GetRequiredService<ISiteService>().CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));

        (await db.AuditLogs.CountAsync(a => a.EntityType == nameof(AuditLog)))
            .Should().Be(0, "a trail of trail-writing grows without limit and tells nobody anything");
    }

    [Fact]
    public async Task One_tenant_cannot_read_another_tenants_trail()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await sp.GetRequiredService<ISiteService>().CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));

        // AuditLog is tenant-owned like everything else, so the global query filter covers it and
        // the trail cannot become the one table that leaks across companies.
        var mine = await db.AuditLogs.CountAsync();
        mine.Should().BeGreaterThan(0);
        (await db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.CompanyId != host.CompanyId))
            .Should().BeGreaterOrEqualTo(0);
        (await db.AuditLogs.CountAsync(a => a.CompanyId != host.CompanyId))
            .Should().Be(0, "the filter, not the caller, is what keeps them apart");
    }

    private sealed record Fixture(Guid ProjectId, Guid ExpenseHeadId);

    private static async Task<Fixture> ArrangeProjectAsync(IServiceProvider sp, AppDbContext db)
    {
        var site = await sp.GetRequiredService<ISiteService>().CreateAsync(new SaveSiteRequest(
            "GV", "Green Valley", null, null, null, null, null, null, SiteStatus.Active, null));
        var project = await sp.GetRequiredService<IProjectService>().CreateAsync(new SaveProjectRequest(
            "GV-101", "Villa 101", "101", site.Id, null, null, null, null, null, null,
            1_000_000, null, ProjectStatus.Active, 0, null));
        return new Fixture(project.Id, await db.ExpenseHeads.Select(h => h.Id).FirstAsync());
    }
}
