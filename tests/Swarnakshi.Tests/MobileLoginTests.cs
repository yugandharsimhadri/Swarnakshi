using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Platform;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// A phone number is a second way in. It already picks out the company, so the person can skip the
/// <c>@companycode</c> — but two companies using the same number makes it ambiguous, and that has to
/// fail helpfully rather than log the wrong person in.
/// </summary>
public class MobileLoginTests
{
    private static RegisterCompanyRequest Registration(string code, string username, string? mobile) =>
        new($"{code} Builders", code, username, "Passw0rd!", "Passw0rd!", null, mobile);

    private static async Task<(IAuthService Auth, ICompanyRegistrationService Reg, AppDbContext Db)> ArrangeAsync(TestHost host)
    {
        var sp = host.Scope().ServiceProvider;
        return (sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ICompanyRegistrationService>(),
                sp.GetRequiredService<AppDbContext>());
    }

    [Theory]
    [InlineData("9876543210")]
    [InlineData("+91 98765 43210")]
    [InlineData("+919876543210")]
    [InlineData("098765 43210")]
    [InlineData("(98765) 43210")]
    public async Task Any_way_of_writing_the_number_signs_the_same_person_in(string typed)
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, reg, _) = await ArrangeAsync(host);
        await reg.RegisterAsync(Registration("acme", "ravi", "9876543210"));

        var res = await auth.LoginAsync(new LoginRequest(typed, "Passw0rd!"));

        res.Kind.Should().Be(AuthResponse.TenantKind);
        res.User!.Login.Should().Be("ravi@acme");
        res.User!.Mobile.Should().Be("9876543210");
    }

    [Fact]
    public async Task Username_at_companycode_still_works_unchanged()
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, reg, _) = await ArrangeAsync(host);
        await reg.RegisterAsync(Registration("acme", "ravi", "9876543210"));

        var res = await auth.LoginAsync(new LoginRequest("ravi@acme", "Passw0rd!"));

        res.User!.Login.Should().Be("ravi@acme");
    }

    [Fact]
    public async Task An_unknown_number_fails_the_same_as_a_bad_password()
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, reg, _) = await ArrangeAsync(host);
        await reg.RegisterAsync(Registration("acme", "ravi", "9876543210"));

        var badNumber = () => auth.LoginAsync(new LoginRequest("9000000000", "Passw0rd!"));
        var badPassword = () => auth.LoginAsync(new LoginRequest("9876543210", "nope"));

        (await badNumber.Should().ThrowAsync<AppException>()).And.StatusCode.Should().Be(401);
        (await badPassword.Should().ThrowAsync<AppException>()).Which.Message
            .Should().Be((await badNumber.Should().ThrowAsync<AppException>()).Which.Message,
                "a stranger must not learn a number is registered just from the error");
    }

    [Fact]
    public async Task The_same_number_in_two_companies_asks_for_the_username()
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, reg, _) = await ArrangeAsync(host);
        await reg.RegisterAsync(Registration("acme", "ravi", "9876543210"));
        await reg.RegisterAsync(Registration("zenith", "priya", "9876543210"));

        var act = () => auth.LoginAsync(new LoginRequest("9876543210", "Passw0rd!"));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("more than one company")
            .And.Contain("username");

        // …and the username path still resolves each of them.
        (await auth.LoginAsync(new LoginRequest("ravi@acme", "Passw0rd!"))).User!.Login.Should().Be("ravi@acme");
        (await auth.LoginAsync(new LoginRequest("priya@zenith", "Passw0rd!"))).User!.Login.Should().Be("priya@zenith");
    }

    [Fact]
    public async Task A_bare_word_is_still_read_as_a_platform_username_not_a_number()
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, _, _) = await ArrangeAsync(host);

        var res = await auth.LoginAsync(new LoginRequest("EnterpriseAdmin", "SivAyAAn@HMS"));

        res.Kind.Should().Be(AuthResponse.PlatformKind);
    }

    [Fact]
    public async Task Registration_without_a_contact_number_leaves_mobile_login_off()
    {
        await using var host = await TestHost.CreateAsync();
        var (auth, reg, db) = await ArrangeAsync(host);
        var company = await reg.RegisterAsync(Registration("acme", "ravi", null));

        using var scope = db.BeginTenantScope(company.CompanyId);
        (await db.Users.SingleAsync()).Mobile.Should().BeNull();
    }

    [Fact]
    public async Task A_teammate_added_with_a_mobile_can_sign_in_by_it()
    {
        await using var host = await TestHost.CreateAsync();
        var sp = host.Scope().ServiceProvider;

        // TestHost is already inside the seeded "swarnakshi" tenant, acting as its owner.
        await sp.GetRequiredService<Application.Users.IUserService>().CreateAsync(
            new Application.Users.CreateUserRequest("Suresh", "suresh", "Passw0rd!", UserRole.Supervisor, null, "88888 77777"));

        var res = await sp.GetRequiredService<IAuthService>()
            .LoginAsync(new LoginRequest("8888877777", "Passw0rd!"));

        res.User!.Login.Should().Be("suresh@swarnakshi");
        res.User!.Mobile.Should().Be("8888877777");
    }

    [Fact]
    public async Task Two_users_in_one_company_cannot_share_a_number()
    {
        await using var host = await TestHost.CreateAsync();
        var users = host.Scope().ServiceProvider.GetRequiredService<Application.Users.IUserService>();

        await users.CreateAsync(new Application.Users.CreateUserRequest(
            "First", "first", "Passw0rd!", UserRole.Supervisor, null, "9000000002"));
        var act = () => users.CreateAsync(new Application.Users.CreateUserRequest(
            "Clash", "clash", "Passw0rd!", UserRole.Supervisor, null, "9 000 000 002"));

        (await act.Should().ThrowAsync<AppException>()).Which.Message.Should().Contain("already used");
    }
}
