using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Security;

/// <summary>Central permission keys. Roles map to a default set, which UserPermission rows then add to or take away from.</summary>
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

    /// <summary>
    /// What someone who runs the work on site can do: raise requests, record purchases, keep
    /// projects moving. The company dashboard and the reports are the office's view, not theirs.
    ///
    /// <para>Shared by Supervisor and Engineer rather than written out twice, so the two cannot
    /// drift apart by accident — if they are ever meant to differ, that will be a deliberate edit
    /// splitting this list, not a permission somebody forgot to add to the second one.</para>
    /// </summary>
    private static readonly string[] OnSite =
        [InventoryView, MaterialRequestCreate, PurchaseCreate, ProjectsManage];

    public static IReadOnlyCollection<string> ForRole(UserRole role) => role switch
    {
        // A Sub-Owner is the owner's second pair of hands — a partner or a family member who runs
        // the business when the owner is not there — so they start with everything the Owner has.
        // Starting at almost nothing and granting upward was the wrong way round: it meant every
        // new Sub-Owner was locked out of the job they had just been appointed to do, and stayed
        // that way until somebody noticed and went looking for the permission screen.
        //
        // The Owner can still take individual permissions away per user; see
        // UserService.SetPermissionsAsync, which records those as explicit denials precisely
        // because the base set is now everything.
        UserRole.Owner or UserRole.SubOwner => All,
        UserRole.Supervisor or UserRole.Engineer => OnSite,
        UserRole.Accountant =>
        [
            ExpenseCreate, LabourCreate, ContractManage, ContractorPaymentCreate,
            CustomerPaymentCreate, InventoryView, ReportsView, DashboardView
        ],
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// What a user can actually do: the role's set, then each per-user row applied in turn — a
    /// granted row adds, a denied row takes away.
    ///
    /// <para>One implementation, used both by the token the user signs in with and by the screen
    /// the Owner edits them on. When those two answered separately, the screen could show a set of
    /// ticks that the user's own session disagreed with.</para>
    /// </summary>
    public static IReadOnlyCollection<string> Effective(
        UserRole role, IEnumerable<(string Key, bool Granted)> overrides)
    {
        var set = new HashSet<string>(ForRole(role));
        foreach (var (key, granted) in overrides)
        {
            if (granted) set.Add(key);
            else set.Remove(key);
        }
        return set;
    }
}
