using System.Text.RegularExpressions;
using Cendrite.Domain.Common;
using FluentValidation;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Commands.Signup;

public class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    private const int PublicKeyLength = 32;
    private const int SignatureLength = 64;

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
            .Must(x => IsBase64OfLength(x, PublicKeyLength))
            .WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.AccountPublicKey)).Description);

        RuleFor(x => x.DevicePublicKey)
            .Must(x => IsBase64OfLength(x, PublicKeyLength))
            .WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DevicePublicKey)).Description);

        RuleFor(x => x.ChallengeSignature)
            .Must(x => IsBase64OfLength(x, SignatureLength))
            .WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.ChallengeSignature)).Description);

        RuleFor(x => x.DeviceApproval)
            .Must(x => IsBase64OfLength(x, SignatureLength))
            .WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceApproval)).Description);

        RuleFor(x => x.DeviceAppVersion)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.DeviceAppVersion)).Description)
            .Must(x => x.Length <= 6).WithMessage(Errors.General.ValueTooLarge(nameof(SignupCommand.DeviceAppVersion), 6).Description)
            .Must(x => Regex.IsMatch(x, @"^\d+\.\d{1,3}$")).WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceAppVersion)).Description);

        RuleFor(x => x.DeviceType)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.DeviceType)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DeviceType)).Description);

        RuleFor(x => x.DevicePlatform)
            .NotEmpty().WithMessage(Errors.General.ValueIsEmpty(nameof(SignupCommand.DevicePlatform)).Description)
            .IsInEnum().WithMessage(Errors.General.UnexpectedValue(nameof(SignupCommand.DevicePlatform)).Description);
    }

    private static bool IsBase64OfLength(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128) return false;

        var buffer = new byte[96];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == length;
    }
}
