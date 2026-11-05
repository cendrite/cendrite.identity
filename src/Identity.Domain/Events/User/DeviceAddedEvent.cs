using Identity.Domain.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Domain.Events.User;

public sealed record DeviceAddedEvent(Guid UserId, Guid DeviceId, Guid ConnectionId, DevicePlatform Platform) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
