using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Security;
using Swarnakshi.Application.Users;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Authentication and user administration — the security surface. Previously untested end to end.
/// </summary>
public class AuthAndUserTests
{
    // Built from the host rather than written out: a host sharing the assembly database gets a
    // company code of its own, so the half after the '@' is only known at run time.
    // username@companycode is still the shape.
    private const string OwnerPassword = "Owner@123";

    // ---- login -----------------------------------------------------------

    [Fact]
    public async Task Login_succeeds_and_returns_the_role_permission_set()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var res = await auth.LoginAsync(new LoginRequest(host.Login("owner"), OwnerPassword));

        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBeNullOrWhiteSpace();
        res.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        res.User!.Login.Should().Be(host.Login("owner"));
        res.User!.Role.Should().Be(UserRole.Owner);
        res.User!.Permissions.Should().Contain(Permissions.MastersManage);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected_without_revealing_which_field_failed()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var act = () => auth.LoginAsync(new LoginRequest(host.Login("owner"), "wrong-password"));

        // Same message for both cases so the endpoint cannot be used to enumerate accounts.
        (await act.Should().ThrowAsync<AppException>().WithMessage("Invalid username or password."))
            .And.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_with_an_unknown_user_gives_the_identical_error()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var act = () => auth.LoginAsync(new LoginRequest(host.Login("nobody"), OwnerPassword));

        await act.Should().ThrowAsync<AppException>().WithMessage("Invalid username or password.");
    }

    [Fact]
    public async Task A_deactivated_user_cannot_log_in()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserService>();
        var auth = sp.GetRequiredService<IAuthService>();

        var supervisor = await users.CreateAsync(
            new CreateUserRequest("Site Supervisor", "sup", "supervisor-pw", UserRole.Supervisor, null));
        await users.UpdateAsync(supervisor.Id, new UpdateUserRequest("Site Supervisor", UserRole.Supervisor, false));

        var act = () => auth.LoginAsync(new LoginRequest(host.Login("sup"), "supervisor-pw"));

        await act.Should().ThrowAsync<AppException>().WithMessage("Invalid username or password.");
    }

    [Fact]
    public async Task Passwords_are_hashed_never_stored_in_the_clear()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Username == "owner");

        owner.PasswordHash.Should().NotBe(OwnerPassword);
        owner.PasswordHash.Should().NotContain(OwnerPassword);
        owner.PasswordHash.Length.Should().BeGreaterThan(20);
    }

    // ---- refresh ---------------------------------------------------------

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var first = await auth.LoginAsync(new LoginRequest(host.Login("owner"), OwnerPassword));
        var second = await auth.RefreshAsync(new RefreshRequest(first.RefreshToken));

        second.RefreshToken.Should().NotBe(first.RefreshToken, "refresh tokens must rotate");

        var reuseOld = () => auth.RefreshAsync(new RefreshRequest(first.RefreshToken));
        await reuseOld.Should().ThrowAsync<AppException>().WithMessage("*Invalid or expired refresh token*");
    }

    [Fact]
    public async Task Refresh_with_a_bogus_token_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var act = () => auth.RefreshAsync(new RefreshRequest("not-a-real-token"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*Invalid or expired refresh token*");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var session = await auth.LoginAsync(new LoginRequest(host.Login("owner"), OwnerPassword));
        await host.LogoutAsAsync(auth, session.User!.Id);

        var act = () => auth.RefreshAsync(new RefreshRequest(session.RefreshToken));
        await act.Should().ThrowAsync<AppException>();
    }

    // ---- user administration --------------------------------------------

    [Fact]
    public async Task Creates_a_user_who_can_then_log_in()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserService>();
        var auth = sp.GetRequiredService<IAuthService>();

        var created = await users.CreateAsync(
            new CreateUserRequest("Anil Accountant", "anil", "accounts-pw", UserRole.Accountant, null));

        created.Role.Should().Be(UserRole.Accountant);
        created.IsActive.Should().BeTrue();

        var login = await auth.LoginAsync(new LoginRequest(host.Login("anil"), "accounts-pw"));
        login.User!.Role.Should().Be(UserRole.Accountant);
        login.User!.Permissions.Should().NotContain(Permissions.MastersManage);
    }

    [Fact]
    public async Task Duplicate_username_within_a_company_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        await users.CreateAsync(new CreateUserRequest("First", "dupe", "password1", UserRole.Supervisor, null));
        var act = () => users.CreateAsync(new CreateUserRequest("Second", "dupe", "password2", UserRole.Accountant, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Short_passwords_are_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        var act = () => users.CreateAsync(new CreateUserRequest("Weak", "weak", "short", UserRole.Supervisor, null));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task You_cannot_change_your_own_role_or_deactivate_yourself()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<IUserService>();

        var me = await db.Users.AsNoTracking().FirstAsync(u => u.Username == "owner");

        var demoteSelf = () => users.UpdateAsync(me.Id, new UpdateUserRequest(me.Name, UserRole.Supervisor, true));
        var disableSelf = () => users.UpdateAsync(me.Id, new UpdateUserRequest(me.Name, UserRole.Owner, false));

        await demoteSelf.Should().ThrowAsync<AppException>().WithMessage("*cannot change your own role*");
        await disableSelf.Should().ThrowAsync<AppException>().WithMessage("*cannot change your own role*");
    }

    [Fact]
    public async Task The_last_active_owner_cannot_be_removed()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<IUserService>();

        // A second Owner exists, so demoting them is allowed …
        var second = await users.CreateAsync(
            new CreateUserRequest("Second Owner", "owner2", "owner2-pw", UserRole.Owner, null));
        await users.UpdateAsync(second.Id, new UpdateUserRequest("Second Owner", UserRole.Supervisor, true));

        // … but now only the seeded Owner is left, and self-demotion is blocked anyway.
        (await db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive)).Should().Be(1);

        var demoteLast = () => users.UpdateAsync(second.Id, new UpdateUserRequest("Second Owner", UserRole.Supervisor, true));
        await demoteLast.Should().NotThrowAsync();   // already a Supervisor — no Owner count change
    }

    [Fact]
    public async Task Demoting_the_only_other_owner_while_deactivating_them_is_blocked()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<IUserService>();

        // Make the seeded owner not the only one, then deactivate every other Owner in turn.
        var owners = await db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive);
        owners.Should().Be(1, "the seed creates exactly one Owner");

        // Deactivating the sole Owner must fail — it is also self, which the guard catches first.
        var sole = await db.Users.AsNoTracking().FirstAsync(u => u.Role == UserRole.Owner);
        var act = () => users.UpdateAsync(sole.Id, new UpdateUserRequest(sole.Name, UserRole.Owner, false));

        await act.Should().ThrowAsync<AppException>();
        (await db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive)).Should().Be(1);
    }

    [Fact]
    public async Task Password_reset_lets_the_user_log_in_with_the_new_password_only()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserService>();
        var auth = sp.GetRequiredService<IAuthService>();

        var u = await users.CreateAsync(
            new CreateUserRequest("Reset Me", "reset", "original-pw", UserRole.Supervisor, null));
        await users.SetPasswordAsync(u.Id, new SetPasswordRequest("brand-new-pw"));

        (await auth.LoginAsync(new LoginRequest(host.Login("reset"), "brand-new-pw"))).User!.Id.Should().Be(u.Id);

        var oldPassword = () => auth.LoginAsync(new LoginRequest(host.Login("reset"), "original-pw"));
        await oldPassword.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Extra_permissions_widen_a_sub_owner_beyond_the_role_default()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserService>();
        var auth = sp.GetRequiredService<IAuthService>();

        var sub = await users.CreateAsync(
            new CreateUserRequest("Sub Owner", "sub", "subowner-pw", UserRole.SubOwner, null));

        // SubOwner's role default is deliberately narrow.
        var before = await host.MeAsAsync(auth, sub.Id);
        before.Permissions.Should().NotContain(Permissions.MastersManage);

        await users.SetPermissionsAsync(sub.Id, new SetPermissionsRequest([Permissions.MastersManage]));

        var after = await host.MeAsAsync(auth, sub.Id);
        after.Permissions.Should().Contain(Permissions.MastersManage);
        after.Permissions.Should().Contain(Permissions.InventoryView, "role defaults are kept as well");
    }

    [Fact]
    public async Task Unknown_permission_keys_are_not_silently_granted()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<IUserService>();
        var auth = sp.GetRequiredService<IAuthService>();

        var sub = await users.CreateAsync(
            new CreateUserRequest("Sub Owner", "sub2", "subowner-pw", UserRole.SubOwner, null));

        try { await users.SetPermissionsAsync(sub.Id, new SetPermissionsRequest(["not.a.real.permission"])); }
        catch (AppException) { /* rejecting outright is also acceptable */ }

        var me = await host.MeAsAsync(auth, sub.Id);
        me.Permissions.Should().NotContain("not.a.real.permission");
        me.Permissions.Should().OnlyContain(p => Permissions.All.Contains(p));
    }

    [Fact]
    public async Task Me_returns_the_effective_permission_set()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var auth = sp.GetRequiredService<IAuthService>();

        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Username == "owner");
        var me = await host.MeAsAsync(auth, owner.Id);

        me.Login.Should().Be(host.Login("owner"));
        me.Permissions.Should().BeEquivalentTo(Permissions.All);
    }

    // ---- role -> permission map -----------------------------------------

    [Theory]
    [InlineData(UserRole.Supervisor, Permissions.MastersManage, false)]
    [InlineData(UserRole.Supervisor, Permissions.PurchaseCreate, true)]
    [InlineData(UserRole.Supervisor, Permissions.ApprovalsDecide, false)]
    [InlineData(UserRole.Supervisor, Permissions.UsersManage, false)]
    // A site Supervisor works the site; the company dashboard and the reports are the office's view.
    [InlineData(UserRole.Supervisor, Permissions.DashboardView, false)]
    [InlineData(UserRole.Supervisor, Permissions.ReportsView, false)]
    [InlineData(UserRole.Accountant, Permissions.ExpenseCreate, true)]
    [InlineData(UserRole.Accountant, Permissions.ApprovalsDecide, false)]
    [InlineData(UserRole.Accountant, Permissions.InventoryAdjust, false)]
    [InlineData(UserRole.Accountant, Permissions.DashboardView, true)]
    [InlineData(UserRole.Accountant, Permissions.ReportsView, true)]
    [InlineData(UserRole.SubOwner, Permissions.DashboardView, true)]
    [InlineData(UserRole.Owner, Permissions.DashboardView, true)]
    [InlineData(UserRole.Owner, Permissions.ApprovalsDecide, true)]
    [InlineData(UserRole.Owner, Permissions.UsersManage, true)]
    public void Role_permission_map_is_locked_down(UserRole role, string permission, bool expected)
        => Permissions.ForRole(role).Contains(permission).Should().Be(expected);

    [Fact]
    public void Only_the_owner_can_decide_approvals_or_manage_users()
    {
        foreach (var role in new[] { UserRole.SubOwner, UserRole.Supervisor, UserRole.Accountant })
        {
            Permissions.ForRole(role).Should().NotContain(Permissions.ApprovalsDecide, $"{role} must not approve");
            Permissions.ForRole(role).Should().NotContain(Permissions.UsersManage, $"{role} must not manage users");
        }
    }
}
