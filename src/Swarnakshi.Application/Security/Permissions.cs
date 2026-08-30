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

    public static readonly IReadOnlyList<string> All =
    [
        MastersManage, SitesManage, ProjectsManage, InventoryView, InventoryAdjust,
        MaterialRequestCreate, PurchaseCreate, ExpenseCreate, LabourCreate, ContractManage,
        ContractorPaymentCreate, CustomerPaymentCreate, ApprovalsDecide, UsersManage,
        SettingsManage, ReportsView
    ];

    public static IReadOnlyCollection<string> ForRole(UserRole role) => role switch
    {
        UserRole.Owner => All,
        UserRole.SubOwner => [InventoryView, ReportsView], // extend per-user via UserPermission
        UserRole.Supervisor =>
        [
            InventoryView, MaterialRequestCreate, PurchaseCreate, ProjectsManage, ReportsView
        ],
        UserRole.Accountant =>
        [
            ExpenseCreate, LabourCreate, ContractManage, ContractorPaymentCreate,
            CustomerPaymentCreate, InventoryView, ReportsView
        ],
        _ => Array.Empty<string>()
    };
}
