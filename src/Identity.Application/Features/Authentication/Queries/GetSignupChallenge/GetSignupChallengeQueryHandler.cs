using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Settings;
using Microsoft.Extensions.Options;

namespace Identity.Application.Features.Authentication.Queries.GetSignupChallenge;

public sealed class GetSignupChallengeQueryHandler(
    ISignupChallengeCache signupChallengeCache,
    IOptions<SignupChallengeSettings> settings)
    : IQueryHandler<GetSignupChallengeQuery, GetSignupChallengeDto>
{
    public async ValueTask<Result<GetSignupChallengeDto>> Handle(GetSignupChallengeQuery q, CancellationToken ct)
    {
        var challenge = SignupChallenge.Create(settings.Value.Difficulty, settings.Value.Lifetime);

        await signupChallengeCache.AddChallengeAsync(challenge, ct);

        return Result.Ok(GetSignupChallengeDto.Map(challenge));
    }
}
