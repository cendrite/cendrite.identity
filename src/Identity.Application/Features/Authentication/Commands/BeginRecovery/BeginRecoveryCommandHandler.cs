using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Domain.Common;

namespace Identity.Application.Features.Authentication.Commands.BeginRecovery;

public sealed class BeginRecoveryCommandHandler(
    IRecoveryChallengeCache recoveryChallengeCache,
    IRateLimiter rateLimiter)
    : ICommandHandler<BeginRecoveryCommand, BeginRecoveryDto>
{
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);
    private const int RateLimitCount = 10;

    public async ValueTask<Result<BeginRecoveryDto>> Handle(BeginRecoveryCommand cmd, CancellationToken ct)
    {
        if (await rateLimiter.IsAllowedAsync($"recovery:begin:{cmd.AccountPublicKey}", RateLimitCount, RateLimitWindow, ct) is false)
            return Result.Fail<BeginRecoveryDto>(Errors.General.TooManyRequests());

        var challenge = RecoveryChallenge.Create(cmd.AccountPublicKey);
        await recoveryChallengeCache.AddChallengeAsync(challenge, ct);

        return Result.Ok(new BeginRecoveryDto(challenge.Id, challenge.ChallengeValue, challenge.ExpiresAt));
    }
}
