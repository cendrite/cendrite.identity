using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Commands.RotateAccountKey;

public class RotateAccountKeyCommandValidator : AbstractValidator<RotateAccountKeyCommand>
{
    public RotateAccountKeyCommandValidator()
    {
        RuleFor(x => x.NewAccountPublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(RotateAccountKeyCommand.NewAccountPublicKey)).Description);

        RuleFor(x => x.Proof)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(RotateAccountKeyCommand.Proof)).Description);

        RuleFor(x => x.Approval)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(RotateAccountKeyCommand.Approval)).Description);
    }
}
