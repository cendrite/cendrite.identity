using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using Identity.Application.Abstractions;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Events;

public sealed class AccountKeyRotatedEventHandler(
    IProducer<AccountKeyRotatedMessage> producer,
    ILogger<AccountKeyRotatedEventHandler> logger)
    : IDomainEventHandler<AccountKeyRotatedEvent>
{
    public async ValueTask Handle(AccountKeyRotatedEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await producer.PublishMessageAsync(new AccountKeyRotatedMessage
            {
                CorrelationId = Guid.NewGuid(),
                UserId = domainEvent.UserId,
                DeviceId = domainEvent.DeviceId,
                ConnectionId = domainEvent.ConnectionId,
                DeviceName = domainEvent.DeviceName,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while handling AccountKeyRotatedEvent for UserId: {UserId}, DeviceId: {DeviceId}", domainEvent.UserId, domainEvent.DeviceId);
            throw;
        }
    }
}
