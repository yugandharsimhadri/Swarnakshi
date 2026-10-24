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
    public void An_engineer_and_a_supervisor_differ_in_both_directions()
    {
        var engineer = Permissions.ForRole(UserRole.Engineer);
        var supervisor = Permissions.ForRole(UserRole.Supervisor);

        // The Engineer types the masters he meets on site: a supplier who is not in the system is
        // a purchase he cannot record, and at six in the evening the office is shut.
        engineer.Should().Contain(Permissions.MastersManage);
        supervisor.Should().NotContain(Permissions.MastersManage);

        // Which villas exist is the shape of the business, not a day's work on it.
        engineer.Should().NotContain(Permissions.ProjectsManage);
        supervisor.Should().Contain(Permissions.ProjectsManage);
    }

    [Fact]
    public void An_engineer_can_add_the_parties_he_buys_from_but_not_the_villas_he_builds()
    {
        var engineer = Permissions.ForRole(UserRole.Engineer);

        engineer.Should().Contain(Permissions.MastersManage,
            "contractors, suppliers and materials are entered where the work happens");
        engineer.Should().NotContain([Permissions.ProjectsManage, Permissions.SitesManage]);
    }

    [Fact]
    public void The_staff_register_is_not_an_engineers_business()
    {
        // It carries what his colleagues earn. Splitting it out of MastersManage is what makes it
        // possible to let him add a supplier without also showing him the payroll.
        Permissions.ForRole(UserRole.Engineer).Should().NotContain(Permissions.EmployeesManage);
        Permissions.ForRole(UserRole.Supervisor).Should().NotContain(Permissions.EmployeesManage);

        Permissions.ForRole(UserRole.Owner).Should().Contain(Permissions.EmployeesManage);
        Permissions.ForRole(UserRole.SubOwner).Should().Contain(Permissions.EmployeesManage);
        // The accountant pays them, so they have to be able to see them.
        Permissions.ForRole(UserRole.Accountant).Should().Contain(Permissions.EmployeesManage);
    }

    [Fact]
    public void An_engineer_runs_the_work_and_does_not_see_the_companys_money()
    {
        var engineer = Permissions.ForRole(UserRole.Engineer);

        engineer.Should().Contain([
            Permissions.InventoryView, Permissions.MaterialRequestCreate,
            Permissions.PurchaseCreate, Permissions.ExpenseCreate
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
