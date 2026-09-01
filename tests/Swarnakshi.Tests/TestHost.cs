using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Swarnakshi.Infrastructure.Persistence.Seed;
using Swarnakshi.Infrastructure.Services;
using Swarnakshi.Infrastructure.Storage;

namespace Swarnakshi.Tests;

/// <summary>Spins up the real Application services over a fresh SQLite in-memory database with master data seeded.</summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }

    /// <summary>The very instance registered in DI — mutate it to change the acting user's role
    /// or permissions mid-test.</summary>
    public FakeCurrentUser CurrentUser { get; }

    private readonly string _storageRoot;

    /// <summary>The tenant every test in this host writes into.</summary>
    public Guid CompanyId { get; }

    private TestHost(SqliteConnection connection, ServiceProvider services, FakeCurrentUser currentUser,
        string storageRoot, Guid companyId)
    {
        _connection = connection;
        Services = services;
        CurrentUser = currentUser;
        _storageRoot = storageRoot;
        CompanyId = companyId;
    }

    public static async Task<TestHost> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var currentUser = new FakeCurrentUser();
        var svc = new ServiceCollection();
        svc.AddSingleton<ICurrentUser>(currentUser);
        svc.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        svc.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        svc.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        svc.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        svc.AddScoped<ITransactionSequenceService, TransactionSequenceService>();
        // Auth needs a real token service; the key is test-only and never leaves this process.
        svc.AddSingleton(new JwtOptions
        {
            Issuer = "Swarnakshi.Tests",
            Audience = "Swarnakshi.Tests",
            Key = "test-only-signing-key-not-used-anywhere-else-0123456789",
            AccessTokenMinutes = 60,
            RefreshTokenDays = 7
        });
        svc.AddScoped<IJwtTokenService, JwtTokenService>();
        // Attachments write to a throwaway folder that DisposeAsync removes.
        var storageRoot = Path.Combine(Path.GetTempPath(), "swarnakshi-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);
        svc.AddSingleton<IFileStorage>(new LocalFileStorage(storageRoot));
        svc.AddSingleton<Swarnakshi.Application.Platform.IRegistrationPolicy>(
            new Swarnakshi.Infrastructure.RegistrationPolicy(30));
        svc.AddScoped<Swarnakshi.Application.Platform.ICompanyProvisioner, CompanyProvisioner>();
        svc.AddApplication();

        var provider = svc.BuildServiceProvider();

        // Every test runs inside one tenant, seeded the same way registration seeds a real one.
        // CurrentUser is set BEFORE any tenant write so the query filter and the insert stamp agree.
        var seedOptions = new PlatformSeedOptions();
        Guid companyId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            companyId = await PlatformSeeder.RunAsync(db, hasher, seedOptions, clock.Today);
            using (db.BeginTenantScope(companyId))
                await MasterDataSeeder.RunAsync(db);
        }

        var host = new TestHost(connection, provider, currentUser, storageRoot, companyId);
        // Act as the seeded owner so writes have both a CreatedBy and a tenant.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = db.Users.IgnoreQueryFilters().First(u => u.CompanyId == companyId);
            currentUser.SetUser(owner.Id, companyId, owner.Role, Application.Security.Permissions.All);
        }
        return host;
    }

    public IServiceScope Scope() => Services.CreateScope();

    /// <summary>Acts as another user of the same tenant — the services now read identity from ICurrentUser.</summary>
    public void ActAs(Guid userId, UserRole role = UserRole.Owner, IEnumerable<string>? permissions = null)
        => CurrentUser.SetUser(userId, CompanyId, role, permissions ?? Application.Security.Permissions.All);

    public async Task<Application.Auth.AuthUserDto> MeAsAsync(Application.Auth.IAuthService auth, Guid userId)
    {
        ActAs(userId);
        return (await auth.MeAsync()).User!;
    }

    public async Task LogoutAsAsync(Application.Auth.IAuthService auth, Guid userId)
    {
        ActAs(userId);
        await auth.LogoutAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true); } catch { /* best effort */ }
    }
}

public sealed class FakeCurrentUser : ICurrentUser
{
    private string[] _permissions = [];
    public Guid? UserId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public string? Username { get; private set; } = "owner";
    public UserRole? Role { get; private set; }
    public bool IsAuthenticated => UserId is not null;
    public IReadOnlyCollection<string> Permissions => _permissions;
    public bool Has(string permissionKey) => !IsPlatformAdmin && _permissions.Contains(permissionKey);

    public void SetUser(Guid id, Guid companyId, UserRole role, IEnumerable<string> permissions, string username = "owner")
    {
        UserId = id;
        CompanyId = companyId;
        IsPlatformAdmin = false;
        Username = username;
        Role = role;
        _permissions = permissions.ToArray();
    }

    /// <summary>Acts as an EnterpriseAdmin: no company, so every tenant query filter excludes it.</summary>
    public void SetPlatformUser(Guid id, string username = "enterpriseadmin")
    {
        UserId = id;
        CompanyId = null;
        IsPlatformAdmin = true;
        Username = username;
        Role = null;
        _permissions = [];
    }
}
