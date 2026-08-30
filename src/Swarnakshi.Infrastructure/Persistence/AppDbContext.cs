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
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserSiteAssignment> UserSiteAssignments => Set<UserSiteAssignment>();

    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<MaterialSubcategory> MaterialSubcategories => Set<MaterialSubcategory>();
    public DbSet<Material> Materials => Set<Material>();
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

    public DbSet<ProjectExpense> ProjectExpenses => Set<ProjectExpense>();
    public DbSet<LabourEntry> LabourEntries => Set<LabourEntry>();

    public DbSet<ContractWork> ContractWorks => Set<ContractWork>();
    public DbSet<ContractorPayment> ContractorPayments => Set<ContractorPayment>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TransactionSequence> TransactionSequences => Set<TransactionSequence>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // SQLite cannot ORDER BY / compare DateTimeOffset natively — store it as UTC ticks (sortable, exact).
        // SQL Server handles DateTimeOffset directly, so this conversion is applied for SQLite only.
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false;
        var dtoConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcDateTime.Ticks,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        var nullableDtoConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcDateTime.Ticks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

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

                if (isSqlite && prop.ClrType == typeof(DateTimeOffset))
                    prop.SetValueConverter(dtoConverter);
                if (isSqlite && prop.ClrType == typeof(DateTimeOffset?))
                    prop.SetValueConverter(nullableDtoConverter);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var uid = currentUser?.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy ??= uid;
            }
            if (entry.State == EntityState.Modified && entry.Entity is AuditableEntity aud)
            {
                aud.ModifiedAt = now;
                aud.ModifiedBy = uid;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
