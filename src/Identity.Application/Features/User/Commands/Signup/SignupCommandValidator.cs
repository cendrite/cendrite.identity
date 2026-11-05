using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Commands.Signup;

public class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.Username)).Description)
            .Must(x => x.Length >= 3).WithMessage(Errors.General.ValueTooSmall(nameof(SignupCommand.Username), 3).Description)
            .Must(x => x.Length <= 20).WithMessage(Errors.General.ValueTooLarge(nameof(SignupCommand.Username), 20).Description);

        RuleFor(x => x.ChallengeId)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.ChallengeId)).Description);

        RuleFor(x => x.AccountPublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.AccountPublicKey)).Description);

        RuleFor(x => x.DevicePublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DevicePublicKey)).Description);

        RuleFor(x => x.ChallengeSignature)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.ChallengeSignature)).Description);

        RuleFor(x => x.DeviceApproval)
            .MustBeSignature().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceApproval)).Description);

        RuleFor(x => x.DeviceAppVersion)
            .MustBeAppVersion().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceAppVersion)).Description);

        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.DeviceType)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceType)).Description);

        RuleFor(x => x.DevicePlatform)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.DevicePlatform)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DevicePlatform)).Description);
    }
}
