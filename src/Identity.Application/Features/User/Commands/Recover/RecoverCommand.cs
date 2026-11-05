using Identity.Application.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Application.Features.User.Commands.Recover;

public sealed record RecoverCommand : ICommand<RecoverDto>
{
    public required string AccountPublicKey { get; init; }
    public required Guid ChallengeId { get; init; }
    public required string ChallengeSignature { get; init; }
    public required string DevicePublicKey { get; init; }
    public required string DeviceApproval { get; init; }
    public required string DeviceAppVersion { get; init; }
    public required DeviceType DeviceType { get; init; }
    public required DevicePlatform DevicePlatform { get; init; }
}
