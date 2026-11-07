using Identity.Domain.Abstractions;

namespace Identity.Domain.Events.User;

public sealed record AccountKeyRotatedEvent(Guid UserId, Guid DeviceId, Guid ConnectionId, string DeviceName) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
