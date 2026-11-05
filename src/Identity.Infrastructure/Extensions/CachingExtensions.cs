using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Infrastructure.Caching;
using Identity.Infrastructure.RateLimiting;
using Identity.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Infrastructure.Extensions;

internal static class CachingExtensions
{
    internal static void AddValkey(this IServiceCollection services)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            var serviceProvider = services.BuildServiceProvider();
            var valkeySettings = serviceProvider.GetRequiredService<IOptions<ValkeySettings>>().Value;

            // Construct the connection string from ValkeySettings
            options.Configuration = $"{valkeySettings.Host}:{valkeySettings.Port}";
            options.InstanceName = "Cendrite.Identity.API_";
        });

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var valkeySettings = sp.GetRequiredService<IOptions<ValkeySettings>>().Value;
            return ConnectionMultiplexer.Connect($"{valkeySettings.Host}:{valkeySettings.Port}");
        });

        services.AddHybridCache();

        services.AddCacheServices();
    }

    private static void AddCacheServices(this IServiceCollection services)
    {
        services.AddSingleton<IValkeyCacheService, ValkeyCacheService>();
        services.AddScoped<INonceCacheService, NonceCacheService>();
        services.AddScoped<ISignupChallengeCache, SignupChallengeCache>();
        services.AddSingleton<IRateLimiter, RedisSlidingWindowRateLimiter>();
    }
}
