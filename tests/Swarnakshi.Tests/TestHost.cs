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

/// <summary>
/// Spins up the real Application services over the assembly's SQL Server database, in a tenant of
/// this host's own with master data seeded.
///
/// <para>SQL Server, not SQLite: the product runs on SQL Server, and a suite that proves the rules
/// hold on a different engine has proved them somewhere nobody deploys. Every host gets a fresh
/// company, and the tenant filter keeps it from seeing any other host's rows — the same isolation
/// the product gives two real customers, exercised a couple of hundred times per run.</para>
/// </summary>
public sealed class TestHost : IAsyncDisposable
{
    public ServiceProvider Services { get; }

    /// <summary>The very instance registered in DI — mutate it to change the acting user's role
    /// or permissions mid-test.</summary>
    public FakeCurrentUser CurrentUser { get; }

    private readonly string _storageRoot;

    /// <summary>Set only for a host that owns its database, and dropped when it is disposed.</summary>
    private readonly string? _ownDatabase;

    /// <summary>The tenant every test in this host writes into.</summary>
    public Guid CompanyId { get; }

    /// <summary>
    /// The tenant's company code — the half after the '@' in a login. Shared hosts get a unique one,
    /// so a test that signs in must build the login from this rather than assume "swarnakshi".
    /// </summary>
    public string CompanyCode { get; }

    private TestHost(ServiceProvider services, FakeCurrentUser currentUser, string storageRoot,
        Guid companyId, string companyCode, string? ownDatabase)
    {
        Services = services;
        CurrentUser = currentUser;
        _storageRoot = storageRoot;
        CompanyId = companyId;
        CompanyCode = companyCode;
        _ownDatabase = ownDatabase;
    }

    /// <summary>The login for a username in this host's tenant.</summary>
    public string Login(string username) => $"{username}@{CompanyCode}";

    /// <summary>
    /// Seeding the platform operator is a write to a row shared by every tenant, so two hosts
    /// starting at once would race to insert the same EnterpriseAdmin. One at a time; it is quick.
    /// </summary>
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    /// <summary>
    /// A host in the assembly's shared database, isolated from every other host by its tenant.
    /// This is what almost every test wants: it costs no schema build, so the suite stays quick.
    /// </summary>
    public static Task<TestHost> CreateAsync() => BuildAsync(null);

    /// <summary>
    /// A host with a database entirely to itself, at the cost of building the schema for it.
    ///
    /// <para>For tests that are about the database rather than about a tenant in it: registering
    /// companies and asserting how many now exist, adopting rows left by the pre-tenancy upgrade,
    /// signing in across two companies. Those read and write platform-level state, which by
    /// definition is not scoped by the tenant filter, so they cannot share.</para>
    /// </summary>
    public static Task<TestHost> CreateIsolatedAsync() => BuildAsync(TestDatabase.CreateOwnAsync());

    private static async Task<TestHost> BuildAsync(Task<string>? ownDatabaseTask)
    {
        var currentUser = new FakeCurrentUser();
        var svc = new ServiceCollection();
        svc.AddSingleton<ICurrentUser>(currentUser);
        var ownDatabase = ownDatabaseTask is null ? null : await ownDatabaseTask;
        var connectionString = ownDatabase is null
            ? TestDatabase.ConnectionString
            : TestDatabase.ConnectionStringFor(ownDatabase);
        svc.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
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
        //
        // The company code is unique per host: the schema has a unique index on it, and every host
        // in the run shares one database, so a fixed "swarnakshi" would collide on the second host.
        // A host with its own database keeps the codebase's real default, so tests that own their
        // database read exactly as they did when every test had one. A shared host cannot: the
        // schema has a unique index on the code, and the seeder would hand the second host
        // "swarnakshi2", which no test would guess.
        var seedOptions = ownDatabase is null
            ? new PlatformSeedOptions { DefaultCompanyCode = $"t{Guid.NewGuid():N}"[..12] }
            : new PlatformSeedOptions();

        Guid companyId;
        string companyCode;
        await SeedGate.WaitAsync();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            if (ownDatabase is not null) await db.Database.EnsureCreatedAsync();

            companyId = await PlatformSeeder.RunAsync(db, hasher, seedOptions, clock.Today);
            companyCode = await db.Companies.IgnoreQueryFilters()
                .Where(c => c.Id == companyId).Select(c => c.Code).FirstAsync();

            using (db.BeginTenantScope(companyId))
                await MasterDataSeeder.RunAsync(db);
        }
        finally { SeedGate.Release(); }

        var host = new TestHost(provider, currentUser, storageRoot, companyId, companyCode, ownDatabase);
        // Act as this tenant's owner so writes have both a CreatedBy and a tenant.
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
        try { if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, true); } catch { /* best effort */ }
        if (_ownDatabase is not null) await TestDatabase.DropOwnAsync(_ownDatabase);
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
