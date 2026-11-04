using Identity.Application.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Application.Features.User.Commands.Signup;

public sealed record SignupCommand : ICommand<SignupDto>
{
    public required string Username { get; init; }
    public required string AccountPublicKey { get; init; }
    public required Guid ChallengeId { get; init; }
    public required ulong PowNonce { get; init; }
    public required string ChallengeSignature { get; init; }
    public required string DevicePublicKey { get; init; }
    public required string DeviceApproval { get; init; }
    public required string DeviceAppVersion { get; init; }
    public required DeviceType DeviceType { get; init; }
    public required DevicePlatform DevicePlatform { get; init; }
}
