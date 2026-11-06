using FluentAssertions;
using Identity.Application.Features.Device.Commands.RedeemLinkCode;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.DeviceTest.CommandTest.RedeemLinkCodeTest;

public class RedeemLinkCodeCommandValidatorTest
{
    private readonly RedeemLinkCodeCommandValidator _sut = new();

    private static RedeemLinkCodeCommand ValidCommand() => new()
    {
        Code = "ABCDE-12345",
        DevicePublicKey = Convert.ToBase64String(new byte[32]),
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
    [InlineData("ABCDE-12345-ABCDE-123")]
    public void Validate_Given_Invalid_Code_Should_Fail(string code)
    {
        _sut.Validate(ValidCommand() with { Code = code }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Bad_Device_Info_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { DevicePublicKey = Convert.ToBase64String(new byte[33]) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DeviceAppVersion = "1.0.0" }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { DevicePlatform = (DevicePlatform)99 }).IsValid.Should().BeFalse();
    }
}
