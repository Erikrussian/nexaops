using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Users.Services;

namespace NexaOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
