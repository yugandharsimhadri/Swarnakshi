using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Swarnakshi.Domain.Entities;

namespace Swarnakshi.Application.Abstractions;

/// <summary>Persistence surface used by the Application layer. Implemented by Infrastructure's AppDbContext.</summary>
public interface IAppDbContext
{
    /// <summary>Tenants. Above the tenant filter — reading this is a platform concern, not a company one.</summary>
    DbSet<Company> Companies { get; }
    DbSet<PlatformUser> PlatformUsers { get; }

    DbSet<User> Users { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<UserSiteAssignment> UserSiteAssignments { get; }

    DbSet<Unit> Units { get; }
    DbSet<MaterialCategory> MaterialCategories { get; }
    DbSet<MaterialSubcategory> MaterialSubcategories { get; }
    DbSet<Material> Materials { get; }
    DbSet<MaterialSpecDefinition> MaterialSpecDefinitions { get; }
    DbSet<MaterialSpecValue> MaterialSpecValues { get; }
    DbSet<ExpenseHead> ExpenseHeads { get; }
    DbSet<ExpenseSubhead> ExpenseSubheads { get; }
    DbSet<LabourCategory> LabourCategories { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<ProjectType> ProjectTypes { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Contractor> Contractors { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Setting> Settings { get; }

    DbSet<Site> Sites { get; }
    DbSet<Project> Projects { get; }

    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }

    DbSet<PurchaseHeader> PurchaseHeaders { get; }
    DbSet<PurchaseItem> PurchaseItems { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<MaterialRequest> MaterialRequests { get; }
    DbSet<MaterialRequestItem> MaterialRequestItems { get; }

    DbSet<Employee> Employees { get; }
    DbSet<EmployeePayment> EmployeePayments { get; }

    DbSet<ProjectExpense> ProjectExpenses { get; }
    DbSet<SiteExpense> SiteExpenses { get; }
    DbSet<LabourEntry> LabourEntries { get; }

    DbSet<ContractWork> ContractWorks { get; }
    DbSet<ContractorPayment> ContractorPayments { get; }
    DbSet<CustomerPayment> CustomerPayments { get; }

    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalHistory> ApprovalHistories { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<TransactionSequence> TransactionSequences { get; }
    DbSet<Attachment> Attachments { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Acts as <paramref name="companyId"/> until the returned scope is disposed: reads are filtered
    /// to it and inserts are stamped with it. Needed where there is no signed-in user yet —
    /// registration seeding — and by the platform console. Restores the previous tenant on dispose.
    /// </summary>
    IDisposable BeginTenantScope(Guid companyId);
}
