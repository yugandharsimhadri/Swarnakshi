using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Auth;

namespace Swarnakshi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<IAuthService>(includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<Sites.ISiteService, Sites.SiteService>();
        services.AddScoped<Projects.IProjectService, Projects.ProjectService>();
        services.AddScoped<Masters.IMasterService, Masters.MasterService>();

        return services;
    }
}
