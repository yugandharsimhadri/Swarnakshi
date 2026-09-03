using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Security;

/// <summary>Central permission keys. Roles map to a default set; SubOwner is customised via UserPermission rows.</summary>
public static class Permissions
{
    public const string MastersManage = "masters.manage";
    public const string SitesManage = "sites.manage";
    public const string ProjectsManage = "projects.manage";

    public const string InventoryView = "inventory.view";
    public const string InventoryAdjust = "inventory.adjust";

    public const string MaterialRequestCreate = "material_request.create";
    public const string PurchaseCreate = "purchase.create";

    public const string ExpenseCreate = "expense.create";
    public const string LabourCreate = "labour.create";
    public const string ContractManage = "contract.manage";
    public const string ContractorPaymentCreate = "contractor_payment.create";
    public const string CustomerPaymentCreate = "customer_payment.create";

    public const string ApprovalsDecide = "approvals.decide";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string ReportsView = "reports.view";

    /// <summary>The company overview screen with its financial KPIs. Not for a site Supervisor —
    /// their day is projects and stock, not the company's money.</summary>
    public const string DashboardView = "dashboard.view";

    public static readonly IReadOnlyList<string> All =
    [
        MastersManage, SitesManage, ProjectsManage, InventoryView, InventoryAdjust,
        MaterialRequestCreate, PurchaseCreate, ExpenseCreate, LabourCreate, ContractManage,
        ContractorPaymentCreate, CustomerPaymentCreate, ApprovalsDecide, UsersManage,
        SettingsManage, ReportsView, DashboardView
    ];

    public static IReadOnlyCollection<string> ForRole(UserRole role) => role switch
    {
        UserRole.Owner => All,
        UserRole.SubOwner => [InventoryView, ReportsView, DashboardView], // extend per-user via UserPermission
        // A Supervisor runs a site: raise requests, record purchases, keep projects moving. The
        // company dashboard and the reports are the office's view, not theirs.
        UserRole.Supervisor =>
        [
            InventoryView, MaterialRequestCreate, PurchaseCreate, ProjectsManage
        ],
        UserRole.Accountant =>
        [
            ExpenseCreate, LabourCreate, ContractManage, ContractorPaymentCreate,
            CustomerPaymentCreate, InventoryView, ReportsView, DashboardView
        ],
        _ => Array.Empty<string>()
    };
}
