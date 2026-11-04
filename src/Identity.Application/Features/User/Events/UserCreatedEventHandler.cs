using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Email;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Events;

public sealed class UserCreatedEventHandler(
    IUserRepository userRepository,
    IProducer<SendEmailMessage> emailProducer,
    ILogger<UserCreatedEventHandler> logger)
    : IDomainEventHandler<UserCreatedEvent>
{
    public async ValueTask Handle(UserCreatedEvent ntf, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserWithVerificationCodesSpec(ntf.UserId), ct);
        if (user is null)
        {
            logger.LogError("User with id {UserId} not found", ntf.UserId);
            throw new InvalidOperationException("User not found");
        }

        var verificationCode = user.GetActiveVerificationCode(UserVerificationCodeType.EmailVerification);
        if (verificationCode is null)
        {
            logger.LogError("Verification code for user with id {UserId} not found", ntf.UserId);
            throw new InvalidOperationException("Verification code not found");
        }

        var emailMessage = new SendEmailMessage
        {
            CorrelationId = Guid.NewGuid(),
            To = user.Email!.Address,
            Subject = "Welcome to Cendrite",
            Body = "Welcome to Cendrite! Here is your verification code: " + verificationCode.Code.Value,
            CausationId = null,
        };

        await emailProducer.PublishMessageAsync(emailMessage, ct);
    }
}
