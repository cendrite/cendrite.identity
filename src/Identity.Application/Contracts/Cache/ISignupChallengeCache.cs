using Identity.Application.Caching.SignupChallenge;

namespace Identity.Application.Contracts.Cache;

public interface ISignupChallengeCache
{
    Task AddChallengeAsync(SignupChallenge challenge, CancellationToken cancellationToken);
    Task<SignupChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task DeleteChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task<bool> TryClaimChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
}
