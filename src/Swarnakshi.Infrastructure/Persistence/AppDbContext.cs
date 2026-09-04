using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? currentUser = null)
    : DbContext(options), IAppDbContext
{
    private Guid? _scopeOverride;
    private bool _hasScopeOverride;

    /// <summary>
    /// The tenant this context is currently acting as. Read by every global query filter and by the
    /// insert stamp in <see cref="SaveChangesAsync"/>. Null means "no tenant" — which filters every
    /// tenant table down to nothing, so an unauthenticated or platform request cannot see company
    /// data by accident.
    ///
    /// Resolved on every read rather than captured in the constructor. A snapshot would freeze
    /// whatever the identity happened to be when the context was first resolved — which, if
    /// anything touches the context before authentication completes, is nobody.
    /// </summary>
    private Guid? CompanyScope => _hasScopeOverride ? _scopeOverride : currentUser?.CompanyId;

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserSiteAssignment> UserSiteAssignments => Set<UserSiteAssignment>();

    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<MaterialSubcategory> MaterialSubcategories => Set<MaterialSubcategory>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialSpecDefinition> MaterialSpecDefinitions => Set<MaterialSpecDefinition>();
    public DbSet<MaterialSpecValue> MaterialSpecValues => Set<MaterialSpecValue>();
    public DbSet<ExpenseHead> ExpenseHeads => Set<ExpenseHead>();
    public DbSet<ExpenseSubhead> ExpenseSubheads => Set<ExpenseSubhead>();
    public DbSet<LabourCategory> LabourCategories => Set<LabourCategory>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<PurchaseHeader> PurchaseHeaders => Set<PurchaseHeader>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    public DbSet<MaterialRequestItem> MaterialRequestItems => Set<MaterialRequestItem>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeePayment> EmployeePayments => Set<EmployeePayment>();

    public DbSet<ProjectExpense> ProjectExpenses => Set<ProjectExpense>();
    public DbSet<SiteExpense> SiteExpenses => Set<SiteExpense>();
    public DbSet<LabourEntry> LabourEntries => Set<LabourEntry>();

    public DbSet<ContractWork> ContractWorks => Set<ContractWork>();
    public DbSet<ContractorPayment> ContractorPayments => Set<ContractorPayment>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TransactionSequence> TransactionSequences => Set<TransactionSequence>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public IDisposable BeginTenantScope(Guid companyId) => new TenantScope(this, companyId);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
        => await ExecuteInTransactionAsync<object?>(async () => { await work(); return null; }, ct);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        // Already inside one — a handler called from the approval queue, say. Joining the caller's
        // transaction is the point: the outer commit decides, and a nested commit here would let
        // half the work survive a later failure.
        if (Database.CurrentTransaction is not null) return await work();

        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            // Dispose would roll the database back on its own. This is the other half: the tracker
            // still holds every modification the database has just thrown away, and a context that
            // disagrees with its own database is a worse problem than the failure that got us here.
            await transaction.RollbackAsync(CancellationToken.None);
            ChangeTracker.Clear();
            throw;
        }
    }

    private sealed class TenantScope : IDisposable
    {
        private readonly AppDbContext _db;
        private readonly Guid? _previousOverride;
        private readonly bool _hadOverride;

        public TenantScope(AppDbContext db, Guid companyId)
        {
            _db = db;
            _hadOverride = db._hasScopeOverride;
            _previousOverride = db._scopeOverride;
            db._scopeOverride = companyId;
            db._hasScopeOverride = true;
        }

        public void Dispose()
        {
            _db._scopeOverride = _previousOverride;
            _db._hasScopeOverride = _hadOverride;
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        var applyFilter = typeof(AppDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var et in b.Model.GetEntityTypes())
        {
            foreach (var prop in et.GetProperties())
            {
                if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                {
                    prop.SetPrecision(18);
                    prop.SetScale(2);
                }
                if (prop.ClrType == typeof(string) && prop.GetMaxLength() is null)
                    prop.SetMaxLength(512);
            }

            // Optimistic concurrency on every transactional (auditable) entity.
            if (typeof(AuditableEntity).IsAssignableFrom(et.ClrType)
                && et.FindProperty(nameof(AuditableEntity.ConcurrencyToken)) is { } tokenProp)
                tokenProp.IsConcurrencyToken = true;

            // Tenant isolation, applied to every ITenantOwned entity rather than one at a time:
            // a new entity is isolated the moment it is added to the model, with nothing to remember.
            if (typeof(ITenantOwned).IsAssignableFrom(et.ClrType) && !et.IsOwned())
            {
                applyFilter.MakeGenericMethod(et.ClrType).Invoke(this, [b]);
                b.Entity(et.ClrType).HasIndex(nameof(ITenantOwned.CompanyId));
            }
        }
    }

    /// <summary>
    /// Closes over <c>this.CompanyScope</c> deliberately: EF resolves a query filter's context
    /// reference against the instance EXECUTING the query, so one cached model serves every tenant.
    /// </summary>
    private void ApplyTenantFilter<TEntity>(ModelBuilder b) where TEntity : class, ITenantOwned
        => b.Entity<TEntity>().HasQueryFilter(e => e.CompanyId == CompanyScope);

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var uid = currentUser?.UserId;
        var audits = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                // Stamp the tenant rather than trusting each service to remember it. Refusing to
                // write an unowned row is the point: a row with no company would be visible to
                // nobody and belong to nobody, and silently losing it is worse than failing here.
                if (entry.Entity.CompanyId == Guid.Empty)
                {
                    entry.Entity.CompanyId = CompanyScope
                        ?? throw new InvalidOperationException(
                            $"Cannot insert {entry.Entity.GetType().Name}: no tenant is in scope. " +
                            "Sign in as a company user, or wrap the write in IAppDbContext.BeginTenantScope.");
                }

                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= uid;
                if (entry.Entity is AuditableEntity)
                    audits.Add(Audit(entry.Entity, "Created", null, uid, now));
            }
            else if (entry.State == EntityState.Modified && entry.Entity is AuditableEntity aud)
            {
                aud.ModifiedAt = now;
                aud.ModifiedBy = uid;
                aud.ConcurrencyToken = Guid.NewGuid(); // EF keeps the loaded value for the WHERE clause

                var statusProp = entry.Property(nameof(AuditableEntity.Status));
                if (statusProp.IsModified && !Equals(statusProp.OriginalValue, statusProp.CurrentValue))
                    audits.Add(Audit(aud, $"Status {statusProp.OriginalValue} -> {statusProp.CurrentValue}", aud.Remarks, uid, now));
            }
        }

        if (audits.Count > 0) AuditLogs.AddRange(audits);
        return base.SaveChangesAsync(ct);
    }

    private static AuditLog Audit(BaseEntity entity, string action, string? data, Guid? uid, DateTimeOffset at) => new()
    {
        CompanyId = entity.CompanyId,
        EntityType = entity.GetType().Name,
        EntityId = entity.Id,
        Action = action,
        DataJson = data,
        UserId = uid,
        At = at
    };
}
