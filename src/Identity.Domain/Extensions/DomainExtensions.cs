using Identity.Domain.Services.User;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Domain.Extensions;

public static class DomainExtensions
{
    public static void AddDomain(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IUserLifeCycleService, UserLifeCycleService>();
        serviceCollection.AddScoped<IAuthenticationService, AuthenticationService>();
        serviceCollection.AddScoped<IDeviceService, DeviceService>();
    }
}
