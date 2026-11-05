using Identity.Application.Caching.RecoveryChallenge;

namespace Identity.Application.Contracts.Cache;

public interface IRecoveryChallengeCache
{
    Task AddChallengeAsync(RecoveryChallenge challenge, CancellationToken cancellationToken);
    Task<RecoveryChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task DeleteChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task<bool> TryClaimChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
}
