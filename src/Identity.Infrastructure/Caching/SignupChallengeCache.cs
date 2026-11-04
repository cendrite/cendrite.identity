using System.Text.Json;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using StackExchange.Redis;

namespace Identity.Infrastructure.Caching;

public class SignupChallengeCache(
    IValkeyCacheService valkeyCacheService,
    IConnectionMultiplexer connectionMultiplexer)
    : ISignupChallengeCache
{
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new SignupChallengeJsonConverter() },
    };

    public Task AddChallengeAsync(SignupChallenge challenge, CancellationToken cancellationToken)
    {
        return valkeyCacheService.SetAsync(challenge.Id.ToString(), challenge, cancellationToken, challenge.ExpiresAt - DateTime.UtcNow);
    }

    public Task<SignupChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        return valkeyCacheService.GetAsync<SignupChallenge>(challengeId.ToString(), _options, cancellationToken);
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

    private static string ClaimKey(Guid challengeId) => $"signup-challenge:claim:{challengeId}";
}
