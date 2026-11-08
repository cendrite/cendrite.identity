using Identity.Application.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Application.Features.Device.Commands.RedeemLinkCode;

public sealed record RedeemLinkCodeCommand : ICommand<RedeemLinkCodeDto>
{
    public required string Code { get; init; }
    public required string DevicePublicKey { get; init; }
    public required string SignalIdentityKey { get; init; }
    public required string DeviceAppVersion { get; init; }
    public required DeviceType DeviceType { get; init; }
    public required DevicePlatform DevicePlatform { get; init; }
}
