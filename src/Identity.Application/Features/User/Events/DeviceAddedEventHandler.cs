using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using Identity.Application.Abstractions;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Events;

public sealed class DeviceAddedEventHandler(
    IProducer<DeviceAddedMessage> producer,
    ILogger<DeviceAddedEventHandler> logger)
    : IDomainEventHandler<DeviceAddedEvent>
{
    public async ValueTask Handle(DeviceAddedEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await producer.PublishMessageAsync(new DeviceAddedMessage
            {
                CorrelationId = Guid.NewGuid(),
                UserId = domainEvent.UserId,
                DeviceId = domainEvent.DeviceId,
                ConnectionId = domainEvent.ConnectionId,
                DevicePlatform = domainEvent.Platform.ToString(),
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while handling DeviceAddedEvent for UserId: {UserId}, DeviceId: {DeviceId}", domainEvent.UserId, domainEvent.DeviceId);
            throw;
        }
    }
}
