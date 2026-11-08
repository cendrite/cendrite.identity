using FluentAssertions;
using Identity.Application.Features.User.Commands.Recover;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.UserTest.CommandTest.RecoverTest;

public class RecoverCommandValidatorTest
{
    private readonly RecoverCommandValidator _sut = new();

    private static string Base64(int length) => Convert.ToBase64String(new byte[length]);

    private static RecoverCommand ValidCommand() => new()
    {
        AccountPublicKey = Base64(32),
        ChallengeId = Guid.NewGuid(),
        ChallengeSignature = Base64(64),
        DevicePublicKey = Base64(32),
        SignalIdentityKey = "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        DeviceApproval = Base64(64),
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Linux,
    };

    [Fact]
    public void Validate_Given_Valid_Command_Should_Pass()
    {
        _sut.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Bad_Keys_Or_Signatures_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { AccountPublicKey = "not base64!" }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DevicePublicKey = Base64(33) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { ChallengeSignature = Base64(32) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DeviceApproval = Base64(65) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Bad_Device_Info_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { ChallengeId = Guid.Empty }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DeviceAppVersion = "1.0.0" }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DevicePlatform = (DevicePlatform)99 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Bad_Signal_Identity_Key_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { SignalIdentityKey = Convert.ToBase64String(new byte[33]) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { SignalIdentityKey = Convert.ToBase64String(new byte[32]) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { SignalIdentityKey = "not base64!" }).IsValid.Should().BeFalse();
    }
}
