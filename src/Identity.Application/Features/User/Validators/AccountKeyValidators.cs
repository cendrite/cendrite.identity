using System.Text.RegularExpressions;
using FluentValidation;

namespace Identity.Application.Features.User.Validators;

public static class AccountKeyValidators
{
    public const int PublicKeyLength = 32;
    public const int SignatureLength = 64;

    public static IRuleBuilderOptions<T, string> MustBePublicKey<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => IsBase64OfLength(value, PublicKeyLength));

    public static IRuleBuilderOptions<T, string> MustBeSignature<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => IsBase64OfLength(value, SignatureLength));

    public static IRuleBuilderOptions<T, string> MustBeAppVersion<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(value => !string.IsNullOrEmpty(value) && value.Length <= 6 && Regex.IsMatch(value, @"^\d+\.\d{1,3}$"));

    private static bool IsBase64OfLength(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128) return false;

        var buffer = new byte[96];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == length;
    }
}
