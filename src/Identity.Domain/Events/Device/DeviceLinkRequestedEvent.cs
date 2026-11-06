using Identity.Domain.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Domain.Events.Device;

public sealed record DeviceLinkRequestedEvent(
    Guid UserId,
    Guid IssuingDeviceId,
    Guid IssuingConnectionId,
    Guid PendingLinkId,
    string DevicePublicKey,
    DevicePlatform DevicePlatform) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
