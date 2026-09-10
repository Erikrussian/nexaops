using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.AuditLogs.Services;
using NexaOps.Application.Auth.Services;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Common.Services;
using NexaOps.Application.Companies.Services;
using NexaOps.Application.CompanyMembers.Services;
using NexaOps.Application.Departments.Services;
using NexaOps.Application.Forms.Services;
using NexaOps.Application.Users.Services;

namespace NexaOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICompanyAccessService, CompanyAccessService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<ICompanyMemberService, CompanyMemberService>();
        services.AddScoped<IFormValidatorService, FormValidatorService>();
        services.AddScoped<IFormService, FormService>();
        services.AddScoped<IFormSubmissionService, FormSubmissionService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        return services;
    }
}
