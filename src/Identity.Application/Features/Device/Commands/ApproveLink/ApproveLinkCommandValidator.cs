using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Commands.ApproveLink;

public class ApproveLinkCommandValidator : AbstractValidator<ApproveLinkCommand>
{
    public ApproveLinkCommandValidator()
    {
        RuleFor(x => x.PendingLinkId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(ApproveLinkCommand.PendingLinkId)).Description);

        RuleFor(x => x.Approval)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(ApproveLinkCommand.Approval)).Description);
    }
}
