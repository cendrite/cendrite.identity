using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using Identity.Application.Abstractions;
using Identity.Domain.Events.Device;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Events;

public sealed class DeviceLinkRequestedEventHandler(
    IProducer<DeviceLinkRequestedMessage> producer,
    ILogger<DeviceLinkRequestedEventHandler> logger)
    : IDomainEventHandler<DeviceLinkRequestedEvent>
{
    public async ValueTask Handle(DeviceLinkRequestedEvent ntf, CancellationToken ct)
    {
        try
        {
            await producer.PublishMessageAsync(new DeviceLinkRequestedMessage
            {
                CorrelationId = Guid.NewGuid(),
                UserId = ntf.UserId,
                IssuingDeviceId = ntf.IssuingDeviceId,
                IssuingConnectionId = ntf.IssuingConnectionId,
                PendingLinkId = ntf.PendingLinkId,
                DevicePublicKey = ntf.DevicePublicKey,
                DevicePlatform = ntf.DevicePlatform.ToString(),
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while handling DeviceLinkRequestedEvent for PendingLinkId: {PendingLinkId}", ntf.PendingLinkId);
            throw;
        }
    }
}
