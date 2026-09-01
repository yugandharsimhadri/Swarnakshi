using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Auth;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Customers;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Application.Procurement;

namespace Swarnakshi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<IAuthService>(includeInternalTypes: true);

        // cross-cutting
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IProjectCostWriter, ProjectCostWriter>();

        // P0
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<Sites.ISiteService, Sites.SiteService>();
        services.AddScoped<Projects.IProjectService, Projects.ProjectService>();
        services.AddScoped<Masters.IMasterService, Masters.MasterService>();
        services.AddScoped<Masters.IMaterialService, Masters.MaterialService>();
        services.AddScoped<Masters.IPartyService, Masters.PartyService>();
        services.AddScoped<Masters.ISimpleMasterService, Masters.SimpleMasterService>();

        // P1 — inventory + procurement + approvals
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(sp => sp.GetRequiredService<InventoryService>());
        services.AddScoped<IInventoryLedger>(sp => sp.GetRequiredService<InventoryService>());

        services.AddScoped<PurchasePoster>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<MaterialRequestIssuer>();
        services.AddScoped<IMaterialRequestService, MaterialRequestService>();

        // P2 — expenses, labour, contractors
        services.AddScoped<IProjectExpenseService, ProjectExpenseService>();
        services.AddScoped<ILabourService, LabourService>();
        services.AddScoped<IContractWorkService, ContractWorkService>();
        services.AddScoped<IContractorPaymentService, ContractorPaymentService>();

        // P3 — customers
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();

        // P4 — dashboard + reports
        services.AddScoped<Dashboard.IDashboardService, Dashboard.DashboardService>();
        services.AddScoped<Reports.IReportsService, Reports.ReportsService>();

        // P5 — attachments + users
        services.AddScoped<Attachments.IAttachmentService, Attachments.AttachmentService>();
        services.AddScoped<Users.IUserService, Users.UserService>();

        // SaaS — tenant registration and the platform (EnterpriseAdmin) console
        services.AddScoped<Platform.ICompanyRegistrationService, Platform.CompanyRegistrationService>();
        services.AddScoped<Platform.IPlatformAdminService, Platform.PlatformAdminService>();

        // approval handlers
        services.AddScoped<IApprovalHandler, PurchaseApprovalHandler>();
        services.AddScoped<IApprovalHandler, MaterialRequestApprovalHandler>();
        services.AddScoped<IApprovalHandler, LabourApprovalHandler>();
        services.AddScoped<IApprovalHandler, ContractorPaymentApprovalHandler>();

        return services;
    }
}
