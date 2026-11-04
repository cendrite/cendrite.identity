using Identity.Application.Abstractions;

namespace Identity.Application.Features.Authentication.Queries.GetSignupChallenge;

public sealed record GetSignupChallengeQuery : IQuery<GetSignupChallengeDto>;
