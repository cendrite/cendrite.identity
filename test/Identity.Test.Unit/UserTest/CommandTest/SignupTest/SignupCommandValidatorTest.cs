using FluentAssertions;
using Identity.Application.Features.User.Commands.Signup;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.UserTest.CommandTest.SignupTest;

public class SignupCommandValidatorTest
{
    private readonly SignupCommandValidator _sut = new();

    private static string Base64(int length) => Convert.ToBase64String(new byte[length]);

    private static SignupCommand ValidCommand() => new()
    {
        Username = "validUser",
        AccountPublicKey = Base64(32),
        ChallengeId = Guid.NewGuid(),
        PowNonce = 42,
        ChallengeSignature = Base64(64),
        DevicePublicKey = Base64(32),
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

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("abcdefghijklmnopqrstu")]
    public void Validate_Given_Invalid_Username_Should_Fail(string username)
    {
        _sut.Validate(ValidCommand() with { Username = username }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Empty_ChallengeId_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { ChallengeId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("")]
    [InlineData("AAAA")]
    public void Validate_Given_Invalid_AccountPublicKey_Should_Fail(string key)
    {
        _sut.Validate(ValidCommand() with { AccountPublicKey = key }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Wrong_Length_DevicePublicKey_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { DevicePublicKey = Base64(33) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Wrong_Length_Signatures_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { ChallengeSignature = Base64(32) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DeviceApproval = Base64(65) }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("12345.1")]
    public void Validate_Given_Invalid_DeviceAppVersion_Should_Fail(string version)
    {
        _sut.Validate(ValidCommand() with { DeviceAppVersion = version }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Unknown_Device_Enums_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { DeviceType = (DeviceType)99 }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DevicePlatform = (DevicePlatform)99 }).IsValid.Should().BeFalse();
    }
}
