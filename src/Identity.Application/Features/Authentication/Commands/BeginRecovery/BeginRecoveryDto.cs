namespace Identity.Application.Features.Authentication.Commands.BeginRecovery;

public sealed record BeginRecoveryDto(Guid ChallengeId, string Challenge, DateTime ExpiresAt);
