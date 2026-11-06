using Cendrite.Domain.Common;
using FluentValidation;
using Identity.Application.Features.User.Validators;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Commands.Link;

public class RedeemLinkCodeCommandValidator : AbstractValidator<RedeemLinkCodeCommand>
{
    public RedeemLinkCodeCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RedeemLinkCodeCommand.Code)).Description)
            .Must(x => x.Length <= 20).WithMessage(Errors.General.ValueTooLarge(nameof(RedeemLinkCodeCommand.Code), 20).Description);

        RuleFor(x => x.DevicePublicKey)
            .MustBePublicKey().WithMessage(Errors.General.UnexpectedValue(nameof(RedeemLinkCodeCommand.DevicePublicKey)).Description);

        RuleFor(x => x.DeviceAppVersion)
            .MustBeAppVersion().WithMessage(Errors.General.UnexpectedValue(nameof(RedeemLinkCodeCommand.DeviceAppVersion)).Description);

        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RedeemLinkCodeCommand.DeviceType)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(RedeemLinkCodeCommand.DeviceType)).Description);

        RuleFor(x => x.DevicePlatform)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(RedeemLinkCodeCommand.DevicePlatform)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(RedeemLinkCodeCommand.DevicePlatform)).Description);
    }
}
