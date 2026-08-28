using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Auth.Services;
using NexaOps.Application.Companies.Services;
using NexaOps.Application.Users.Services;

namespace NexaOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}
