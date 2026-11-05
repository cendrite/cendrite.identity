using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.Authentication.Commands.BeginRecovery;

public class BeginRecoveryCommandValidator : AbstractValidator<BeginRecoveryCommand>
{
    public BeginRecoveryCommandValidator()
    {
        RuleFor(x => x.AccountPublicKey)
            .MustBePublicKey()
            .WithMessage(Errors.General.UnexpectedValue(nameof(BeginRecoveryCommand.AccountPublicKey)).Description);
    }
}
