using FluentAssertions;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Enums;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// What each role can do. These are cheap assertions about a lookup table, and they are here because
/// the table is the whole of the authorisation model — a permission quietly dropped from a role is
/// not something any other test would notice.
/// </summary>
public class RolePermissionTests
{
    [Fact]
    public void A_sub_owner_can_do_everything_an_owner_can()
    {
        // The Sub-Owner is the owner's second pair of hands - a partner, or family running the
        // business while the owner is away. They used to start with three permissions, which meant
        // every new one was locked out of the job they had just been given.
        Permissions.ForRole(UserRole.SubOwner)
            .Should().BeEquivalentTo(Permissions.ForRole(UserRole.Owner));
    }

    [Fact]
    public void A_sub_owner_can_approve()
    {
        // Worth its own assertion: approving is the one thing the owner most needs covered when
        // they are not there, and it is the permission a narrower default silently withheld.
        Permissions.ForRole(UserRole.SubOwner).Should().Contain(Permissions.ApprovalsDecide);
    }

    [Fact]
    public void An_engineer_gets_the_same_permissions_as_a_supervisor()
    {
        Permissions.ForRole(UserRole.Engineer)
            .Should().BeEquivalentTo(Permissions.ForRole(UserRole.Supervisor));
    }

    [Fact]
    public void An_engineer_runs_the_work_and_does_not_see_the_companys_money()
    {
        var engineer = Permissions.ForRole(UserRole.Engineer);

        engineer.Should().Contain([
            Permissions.InventoryView, Permissions.MaterialRequestCreate,
            Permissions.PurchaseCreate, Permissions.ProjectsManage
        ]);
        engineer.Should().NotContain([
            Permissions.ApprovalsDecide, Permissions.DashboardView,
            Permissions.ReportsView, Permissions.UsersManage
        ]);
    }

    [Fact]
    public void A_denial_row_takes_a_permission_away_from_a_sub_owner()
    {
        // The whole point of writing explicit denials: with a base set of everything, leaving a key
        // out of the list can no longer mean "not granted", so it has to be recorded as refused.
        var effective = Permissions.Effective(UserRole.SubOwner,
            [(Permissions.ApprovalsDecide, false), (Permissions.UsersManage, false)]);

        effective.Should().NotContain(Permissions.ApprovalsDecide);
        effective.Should().NotContain(Permissions.UsersManage);
        effective.Should().Contain(Permissions.ReportsView, "only the two named were taken away");
    }

    [Fact]
    public void A_grant_row_adds_a_permission_to_a_role_that_lacks_it()
    {
        Permissions.ForRole(UserRole.Supervisor).Should().NotContain(Permissions.ReportsView);

        Permissions.Effective(UserRole.Supervisor, [(Permissions.ReportsView, true)])
            .Should().Contain(Permissions.ReportsView);
    }

    [Fact]
    public void Every_role_only_ever_holds_keys_that_exist()
    {
        foreach (var role in Enum.GetValues<UserRole>())
            Permissions.ForRole(role).Should().BeSubsetOf(Permissions.All,
                $"{role} must not carry a permission key nothing checks");
    }
}
