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

    private TestHost(SqliteConnection connection, ServiceProvider services, FakeCurrentUser currentUser,
        string storageRoot)
    {
        _connection = connection;
        Services = services;
        CurrentUser = currentUser;
        _storageRoot = storageRoot;
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
        svc.AddApplication();

        var provider = svc.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            await MasterDataSeeder.RunAsync(db, hasher, "owner@test.local", "pw");
        }

        var host = new TestHost(connection, provider, currentUser, storageRoot);
        // resolve the seeded owner id so writes have a CreatedBy
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = db.Users.First();
            currentUser.SetUser(owner.Id, owner.Role, Application.Security.Permissions.All);
        }
        return host;
    }

    public IServiceScope Scope() => Services.CreateScope();

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
    public string? Email => "owner@test.local";
    public UserRole? Role { get; private set; }
    public bool IsAuthenticated => UserId is not null;
    public IReadOnlyCollection<string> Permissions => _permissions;
    public bool Has(string permissionKey) => _permissions.Contains(permissionKey);

    public void SetUser(Guid id, UserRole role, IEnumerable<string> permissions)
    {
        UserId = id;
        Role = role;
        _permissions = permissions.ToArray();
    }
}
