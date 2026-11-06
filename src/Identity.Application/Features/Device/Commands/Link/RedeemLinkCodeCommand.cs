using Identity.Application.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Device.Commands.Link;

public sealed record RedeemLinkCodeCommand : ICommand<RedeemLinkCodeDto>
{
    public required string Code { get; init; }
    public required string DevicePublicKey { get; init; }
    public required string DeviceAppVersion { get; init; }
    public required DeviceType DeviceType { get; init; }
    public required DevicePlatform DevicePlatform { get; init; }
}

public sealed record RedeemLinkCodeDto(Guid PendingLinkId, string PollSecret, DateTime ExpiresAt);
