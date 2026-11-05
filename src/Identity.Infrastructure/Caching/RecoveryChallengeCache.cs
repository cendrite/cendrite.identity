using System.Text.Json;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using StackExchange.Redis;

namespace Identity.Infrastructure.Caching;

public class RecoveryChallengeCache(
    IValkeyCacheService valkeyCacheService,
    IConnectionMultiplexer connectionMultiplexer)
    : IRecoveryChallengeCache
{
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new RecoveryChallengeJsonConverter() },
    };

    public Task AddChallengeAsync(RecoveryChallenge challenge, CancellationToken cancellationToken)
    {
        return valkeyCacheService.SetAsync(challenge.Id.ToString(), challenge, cancellationToken, challenge.ExpiresAt - DateTime.UtcNow);
    }

    public Task<RecoveryChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        return valkeyCacheService.GetAsync<RecoveryChallenge>(challengeId.ToString(), _options, cancellationToken);
    }

    public Task DeleteChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        return valkeyCacheService.RemoveAsync(challengeId.ToString(), cancellationToken);
    }

    public Task<bool> TryClaimChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        return connectionMultiplexer.GetDatabase()
            .StringSetAsync(ClaimKey(challengeId), "1", ClaimLifetime, When.NotExists);
    }

    private static string ClaimKey(Guid challengeId) => $"recovery-challenge:claim:{challengeId}";
}
