namespace Identity.Application.Features.Authentication.Queries.GetSignupChallenge;

public sealed record GetSignupChallengeDto(Guid ChallengeId, string Challenge, int Difficulty, DateTime ExpiresAt);
