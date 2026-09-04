using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Auth;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// The upgrade path: a database that already held a business before multi-tenancy existed.
///
/// Every other test in this suite starts from an empty database, which is the one path that always
/// worked — the founding-company seed creates its owner because there are no users yet. A real
/// install takes the other path, and it was completely broken: the migration adds Users.Username
/// with an empty default and never backfills it, the seed skips owner creation because users DO
/// exist, and the result is a company whose every user is present, active, and unable to sign in.
///
/// These tests reproduce that database and assert the symptom a person would actually report —
/// "I cannot log in" — rather than the column value behind it.
/// </summary>
public class UpgradeFromSingleTenantTests
{
    /// <summary>
    /// Rewinds the seeded tenant to what the multi-tenancy migration leaves behind: users carried
    /// over with an empty username and nobody marked as the company's admin.
    /// </summary>
    private static async Task<User> MakeLookUpgradedAsync(TestHost host)
    {
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == host.CompanyId);
        user.Username = "";
        user.Email = "owner@swarnakshi.local";
        user.IsCompanyAdmin = false;
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task ReseedAsync(TestHost host)
    {
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        await PlatformSeeder.RunAsync(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IPasswordHasher>(),
            new PlatformSeedOptions(),
            sp.GetRequiredService<IDateTimeProvider>().Today);
    }

    [Fact]
    public async Task An_upgraded_database_leaves_its_users_able_to_sign_in()
    {
        await using var host = await TestHost.CreateIsolatedAsync();
        var user = await MakeLookUpgradedAsync(host);

        await ReseedAsync(host);

        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);

        reloaded.Username.Should().Be("owner",
            "before multi-tenancy the email WAS the login, so its local part is the login the person already knows");
    }

    [Fact]
    public async Task The_adopted_owner_can_administer_the_company_it_was_adopted_into()
    {
        await using var host = await TestHost.CreateIsolatedAsync();
        var user = await MakeLookUpgradedAsync(host);

        await ReseedAsync(host);

        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);

        reloaded.IsCompanyAdmin.Should().BeTrue(
            "IsCompanyAdmin was added defaulting to false, so an adopted company would have nobody able to administer it");
    }

    [Fact]
    public async Task Seeding_an_already_upgraded_database_again_changes_nothing()
    {
        await using var host = await TestHost.CreateIsolatedAsync();
        await MakeLookUpgradedAsync(host);

        await ReseedAsync(host);
        await ReseedAsync(host);

        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users.IgnoreQueryFilters()
            .Where(u => u.CompanyId == host.CompanyId).ToListAsync();

        users.Should().ContainSingle("a second startup must not add a duplicate owner")
            .Which.Username.Should().Be("owner", "an existing login is never rewritten");
    }

    [Fact]
    public async Task Two_users_whose_emails_share_a_local_part_still_get_distinct_logins()
    {
        await using var host = await TestHost.CreateIsolatedAsync();
        await MakeLookUpgradedAsync(host);

        using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var tenant = db.BeginTenantScope(host.CompanyId);
            var second = new User
            {
                Name = "Site Owner",
                Email = "owner@othercompany.example",
                PasswordHash = "x",
                Role = UserRole.Supervisor,
                IsActive = true
            };
            // The row-id placeholder the migration writes, not "" — two empty usernames could never
            // coexist under the unique index, which is the whole reason the placeholder exists.
            second.Username = second.Id.ToString();
            db.Users.Add(second);
            await db.SaveChangesAsync();
        }

        await ReseedAsync(host);

        using var check = host.Scope();
        var users = await check.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.IgnoreQueryFilters().Where(u => u.CompanyId == host.CompanyId).ToListAsync();

        users.Select(u => u.Username).Should().OnlyHaveUniqueItems(
            "username is unique per company, so a collision would fail the insert");
        users.Should().OnlyContain(u => u.Username.Length >= 3);
    }

    [Fact]
    public async Task A_user_with_no_email_at_all_still_gets_a_usable_login()
    {
        await using var host = await TestHost.CreateIsolatedAsync();

        using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == host.CompanyId);
            user.Username = "";
            user.Email = null;
            user.Name = "Ravi Kumar";
            await db.SaveChangesAsync();
        }

        await ReseedAsync(host);

        using var check = host.Scope();
        var reloaded = await check.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == host.CompanyId);

        reloaded.Username.Should().Be("ravikumar", "the name is the only thing left to build a login from");
        Swarnakshi.Application.Platform.LoginIdentity.IsValidUsername(reloaded.Username)
            .Should().BeTrue("a derived login must satisfy the same rules as a typed one");
    }

    /// <summary>
    /// The case that could not even migrate. A UNIQUE index on (CompanyId, Username) is built over a
    /// column this migration adds with an empty default, so a second user made the index creation
    /// fail and the whole upgrade abort at startup. The migration now writes a per-row placeholder
    /// first; this asserts the placeholder never survives as somebody's login.
    /// </summary>
    [Fact]
    public async Task The_row_id_placeholder_the_migration_writes_is_replaced_by_a_real_login()
    {
        await using var host = await TestHost.CreateIsolatedAsync();

        Guid userId;
        using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.CompanyId == host.CompanyId);
            user.Email = "owner@swarnakshi.local";
            user.Username = user.Id.ToString();
            userId = user.Id;
            await db.SaveChangesAsync();
        }

        await ReseedAsync(host);

        using var check = host.Scope();
        var reloaded = await check.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);

        reloaded.Username.Should().Be("owner");
        reloaded.Username.Should().NotBe(userId.ToString(), "a row id is not something a person can type");
    }

    /// <summary>The migration's own guard: the placeholder must satisfy the unique index it enables.</summary>
    [Fact]
    public async Task Every_adopted_user_ends_up_with_a_login_that_is_valid_and_unique()
    {
        await using var host = await TestHost.CreateIsolatedAsync();
        await MakeLookUpgradedAsync(host);

        using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            using var tenant = db.BeginTenantScope(host.CompanyId);
            foreach (var name in new[] { "Ravi Kumar", "Ravi Kumar", "K" })
            {
                var u = new User
                {
                    Name = name, Email = null, PasswordHash = "x",
                    Role = UserRole.Supervisor, IsActive = true
                };
                u.Username = u.Id.ToString();
                db.Users.Add(u);
            }
            await db.SaveChangesAsync();
        }

        await ReseedAsync(host);

        using var check = host.Scope();
        var users = await check.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.IgnoreQueryFilters().Where(u => u.CompanyId == host.CompanyId).ToListAsync();

        users.Should().HaveCount(4);
        users.Select(u => u.Username).Should().OnlyHaveUniqueItems();
        users.Should().OnlyContain(
            u => Swarnakshi.Application.Platform.LoginIdentity.IsValidUsername(u.Username),
            "a derived login must satisfy the same rules as a typed one");
    }
}
