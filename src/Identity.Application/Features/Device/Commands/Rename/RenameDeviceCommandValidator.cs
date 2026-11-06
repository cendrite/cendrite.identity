using FluentValidation;
using Identity.Domain.Common;
using Identity.Domain.Constants;

namespace Identity.Application.Features.Device.Commands.Rename;

public sealed class RenameDeviceCommandValidator : AbstractValidator<RenameDeviceCommand>
{
    public RenameDeviceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.TargetDeviceId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= DeviceConstants.NameMaxLength)
            .WithMessage(Errors.General.ValueOutOfRange(nameof(RenameDeviceCommand.Name), 1, DeviceConstants.NameMaxLength).Description);
    }
}
