using Identity.Domain.Events.User;
using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Events;

public class UserDeletedEventHandler(
    IProducer<UserDeletedMessage> producer,
    ILogger<UserDeletedEventHandler> logger)
    : IDomainEventHandler<UserDeletedEvent>
{
    public async ValueTask Handle(UserDeletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("User with id {UserId} has been deleted", notification.UserId);

        var message = new UserDeletedMessage
        {
            CorrelationId = Guid.NewGuid(),
            UserId = notification.UserId,
            CausationId = null
        };

        await producer.PublishMessageAsync(message, cancellationToken);
    }
}