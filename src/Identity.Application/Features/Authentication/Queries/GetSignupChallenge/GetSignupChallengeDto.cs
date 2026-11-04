using Identity.Application.Caching.SignupChallenge;

namespace Identity.Application.Features.Authentication.Queries.GetSignupChallenge;

public sealed record GetSignupChallengeDto
{
    public Guid ChallengeId { get; private init; }
    public string Challenge { get; private init; } = null!;
    public int Difficulty { get; private init; }
    public DateTime ExpiresAt { get; private init; }

    private GetSignupChallengeDto() { }

    public static GetSignupChallengeDto Map(SignupChallenge challenge)
    {
        return new GetSignupChallengeDto
        {
            ChallengeId = challenge.Id,
            Challenge = challenge.ChallengeValue,
            Difficulty = challenge.Difficulty,
            ExpiresAt = challenge.ExpiresAt,
        };
    }
}
