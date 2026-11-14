using Cendrite.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Common;
using FluentValidation;

namespace Identity.Application.Features.Authentication.Commands.VerifyNonce;

public class VerifyNonceCommandValidator : AbstractValidator<VerifyNonceCommand>
{
    public VerifyNonceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(VerifyNonceCommand.UserId)).Description);
        RuleFor(x => x.NonceId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(VerifyNonceCommand.NonceId)).Description);
        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(VerifyNonceCommand.DeviceId)).Description);
        RuleFor(x => x.Nonce)
            .NotEmpty()
            .WithMessage(Errors.General.ValueIsEmpty(nameof(VerifyNonceCommand.Nonce)).Description);
        RuleFor(x => x.TokenRequests)
            .NotNull()
            .Must(requests => requests.Count <= PrivateTokenConstants.MaxTokenRequests)
            .WithMessage($"At most {PrivateTokenConstants.MaxTokenRequests} token requests can be sent at once.");
    }
}