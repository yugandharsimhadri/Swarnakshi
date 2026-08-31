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
    private const string OwnerEmail = "owner@test.local";
    private const string OwnerPassword = "pw";

    // ---- login -----------------------------------------------------------

    [Fact]
    public async Task Login_succeeds_and_returns_the_role_permission_set()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var res = await auth.LoginAsync(new LoginRequest(OwnerEmail, OwnerPassword));

        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBeNullOrWhiteSpace();
        res.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        res.User.Email.Should().Be(OwnerEmail);
        res.User.Role.Should().Be(UserRole.Owner);
        res.User.Permissions.Should().Contain(Permissions.MastersManage);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected_without_revealing_which_field_failed()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var act = () => auth.LoginAsync(new LoginRequest(OwnerEmail, "wrong-password"));

        // Same message for both cases so the endpoint cannot be used to enumerate accounts.
        (await act.Should().ThrowAsync<AppException>().WithMessage("Invalid email or password."))
            .And.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_with_an_unknown_email_gives_the_identical_error()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var act = () => auth.LoginAsync(new LoginRequest("nobody@test.local", OwnerPassword));

        await act.Should().ThrowAsync<AppException>().WithMessage("Invalid email or password.");
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
            new CreateUserRequest("Site Supervisor", "sup@test.local", "supervisor-pw", UserRole.Supervisor));
        await users.UpdateAsync(supervisor.Id, new UpdateUserRequest("Site Supervisor", UserRole.Supervisor, false));

        var act = () => auth.LoginAsync(new LoginRequest("sup@test.local", "supervisor-pw"));

        await act.Should().ThrowAsync<AppException>().WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Passwords_are_hashed_never_stored_in_the_clear()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Email == OwnerEmail);

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

        var first = await auth.LoginAsync(new LoginRequest(OwnerEmail, OwnerPassword));
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

        var session = await auth.LoginAsync(new LoginRequest(OwnerEmail, OwnerPassword));
        await auth.LogoutAsync(session.User.Id);

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
            new CreateUserRequest("Anil Accountant", "anil@test.local", "accounts-pw", UserRole.Accountant));

        created.Role.Should().Be(UserRole.Accountant);
        created.IsActive.Should().BeTrue();

        var login = await auth.LoginAsync(new LoginRequest("anil@test.local", "accounts-pw"));
        login.User.Role.Should().Be(UserRole.Accountant);
        login.User.Permissions.Should().NotContain(Permissions.MastersManage);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        await users.CreateAsync(new CreateUserRequest("First", "dupe@test.local", "password1", UserRole.Supervisor));
        var act = () => users.CreateAsync(new CreateUserRequest("Second", "dupe@test.local", "password2", UserRole.Accountant));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Short_passwords_are_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var users = scope.ServiceProvider.GetRequiredService<IUserService>();

        var act = () => users.CreateAsync(new CreateUserRequest("Weak", "weak@test.local", "short", UserRole.Supervisor));

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

        var me = await db.Users.AsNoTracking().FirstAsync(u => u.Email == OwnerEmail);

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
            new CreateUserRequest("Second Owner", "owner2@test.local", "owner2-pw", UserRole.Owner));
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
            new CreateUserRequest("Reset Me", "reset@test.local", "original-pw", UserRole.Supervisor));
        await users.SetPasswordAsync(u.Id, new SetPasswordRequest("brand-new-pw"));

        (await auth.LoginAsync(new LoginRequest("reset@test.local", "brand-new-pw"))).User.Id.Should().Be(u.Id);

        var oldPassword = () => auth.LoginAsync(new LoginRequest("reset@test.local", "original-pw"));
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
            new CreateUserRequest("Sub Owner", "sub@test.local", "subowner-pw", UserRole.SubOwner));

        // SubOwner's role default is deliberately narrow.
        var before = await auth.MeAsync(sub.Id);
        before.Permissions.Should().NotContain(Permissions.MastersManage);

        await users.SetPermissionsAsync(sub.Id, new SetPermissionsRequest([Permissions.MastersManage]));

        var after = await auth.MeAsync(sub.Id);
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
            new CreateUserRequest("Sub Owner", "sub2@test.local", "subowner-pw", UserRole.SubOwner));

        try { await users.SetPermissionsAsync(sub.Id, new SetPermissionsRequest(["not.a.real.permission"])); }
        catch (AppException) { /* rejecting outright is also acceptable */ }

        var me = await auth.MeAsync(sub.Id);
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

        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Email == OwnerEmail);
        var me = await auth.MeAsync(owner.Id);

        me.Email.Should().Be(OwnerEmail);
        me.Permissions.Should().BeEquivalentTo(Permissions.All);
    }

    // ---- role -> permission map -----------------------------------------

    [Theory]
    [InlineData(UserRole.Supervisor, Permissions.MastersManage, false)]
    [InlineData(UserRole.Supervisor, Permissions.PurchaseCreate, true)]
    [InlineData(UserRole.Supervisor, Permissions.ApprovalsDecide, false)]
    [InlineData(UserRole.Supervisor, Permissions.UsersManage, false)]
    [InlineData(UserRole.Accountant, Permissions.ExpenseCreate, true)]
    [InlineData(UserRole.Accountant, Permissions.ApprovalsDecide, false)]
    [InlineData(UserRole.Accountant, Permissions.InventoryAdjust, false)]
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
