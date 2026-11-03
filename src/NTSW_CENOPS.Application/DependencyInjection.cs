using NTSW_CENOPS.Application.Authentication;
using NTSW_CENOPS.Application.Monitoring;
using NTSW_CENOPS.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace NTSW_CENOPS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserPasswordService, UserPasswordService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IHealthMonitoringService, HealthMonitoringService>();

        return services;
    }
}
