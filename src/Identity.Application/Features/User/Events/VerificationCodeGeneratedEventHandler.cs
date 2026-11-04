using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Email;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Events;

public class VerificationCodeGeneratedEventHandler(
    IUserRepository userRepository,
    IProducer<SendEmailMessage> emailProducer,
    ILogger<VerificationCodeGeneratedEventHandler> logger)
    : IDomainEventHandler<VerificationCodeGeneratedEvent>
{
    public async ValueTask Handle(VerificationCodeGeneratedEvent ntf, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserWithVerificationCodesSpec(ntf.UserId), ct);
        if (user is null)
        {
            logger.LogError("User with id {UserId} not found", ntf.UserId);
            throw new InvalidOperationException("User not found");
        }

        var verificationCode = user.GetActiveVerificationCode(ntf.CodeType);

        if (verificationCode is null)
        {
            logger.LogError("Verification code for user with id {UserId} not found", ntf.UserId);
            throw new InvalidOperationException("Verification code not found");
        }

        var message = ntf.CodeType switch
        {
            UserVerificationCodeType.Login => "Here is your login verification code: ",
            UserVerificationCodeType.EmailVerification => "Here is your email verification code: ",
            UserVerificationCodeType.PasswordReset => "Here is your password reset verification code: ",
            _ => throw new ArgumentOutOfRangeException(nameof(ntf.CodeType), "Invalid verification code type")
        };

        var emailMessage = new SendEmailMessage()
        {
            CorrelationId = Guid.NewGuid(),
            To = user.Email!.Address,
            Subject = "Cendrite Verification",
            Body = message + verificationCode.Code.Value,
            CausationId = null,
        };

        await emailProducer.PublishMessageAsync(emailMessage, ct);
    }
}
