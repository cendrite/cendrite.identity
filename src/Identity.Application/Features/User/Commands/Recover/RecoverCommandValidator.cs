using Cendrite.Domain.Common;
using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Commands.Recover;

public class RecoverCommandValidator : AbstractValidator<RecoverCommand>
{
    public RecoverCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ChallengeId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RecoverCommand.ChallengeId)).Description);

        RuleFor(x => x.AccountPublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.AccountPublicKey)).Description);

        RuleFor(x => x.DevicePublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.DevicePublicKey)).Description);

        RuleFor(x => x.ChallengeSignature)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.ChallengeSignature)).Description);

        RuleFor(x => x.DeviceApproval)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.DeviceApproval)).Description);

        RuleFor(x => x.DeviceAppVersion)
            .MustBeAppVersion().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.DeviceAppVersion)).Description);

        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RecoverCommand.DeviceType)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.DeviceType)).Description);

        RuleFor(x => x.DevicePlatform)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RecoverCommand.DevicePlatform)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(RecoverCommand.DevicePlatform)).Description);
    }
}
