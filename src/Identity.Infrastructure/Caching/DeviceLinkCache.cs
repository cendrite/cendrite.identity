using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using StackExchange.Redis;

namespace Identity.Infrastructure.Caching;

public class DeviceLinkCache(
    IValkeyCacheService valkeyCacheService,
    IConnectionMultiplexer connectionMultiplexer)
    : IDeviceLinkCache
{
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    public Task AddCodeAsync(LinkCode code, CancellationToken cancellationToken) =>
        valkeyCacheService.SetAsync(CodeKey(code.Code), code, cancellationToken, Remaining(code.ExpiresAt));

    public Task<LinkCode?> GetCodeAsync(string code, CancellationToken cancellationToken) =>
        valkeyCacheService.GetAsync<LinkCode>(CodeKey(code), null, cancellationToken);

    public Task DeleteCodeAsync(string code, CancellationToken cancellationToken) =>
        valkeyCacheService.RemoveAsync(CodeKey(code), cancellationToken);

    public Task<bool> TryClaimCodeAsync(string code, CancellationToken cancellationToken) =>
        connectionMultiplexer.GetDatabase().StringSetAsync($"link-code:claim:{code}", "1", ClaimLifetime, When.NotExists);

    public Task SetPendingAsync(PendingLink pending, CancellationToken cancellationToken) =>
        valkeyCacheService.SetAsync(PendingKey(pending.Id), pending, cancellationToken, Remaining(pending.ExpiresAt));

    public Task<PendingLink?> GetPendingAsync(Guid pendingLinkId, CancellationToken cancellationToken) =>
        valkeyCacheService.GetAsync<PendingLink>(PendingKey(pendingLinkId), null, cancellationToken);

    public Task<bool> TryClaimDecisionAsync(Guid pendingLinkId, CancellationToken cancellationToken) =>
        connectionMultiplexer.GetDatabase().StringSetAsync(DecisionKey(pendingLinkId), "1", ClaimLifetime, When.NotExists);

    public Task ReleaseDecisionAsync(Guid pendingLinkId, CancellationToken cancellationToken) =>
        connectionMultiplexer.GetDatabase().KeyDeleteAsync(DecisionKey(pendingLinkId));

    private static string CodeKey(string code) => $"link-code:{code}";
    private static string PendingKey(Guid id) => $"pending-link:{id}";
    private static string DecisionKey(Guid id) => $"pending-link:decision:{id}";

    private static TimeSpan Remaining(DateTime expiresAt)
    {
        var remaining = expiresAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);
    }
}
