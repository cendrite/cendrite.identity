using FluentAssertions;
using Identity.Application.Features.Device.Commands.Rename;

namespace Identity.Test.Unit.DeviceTest.CommandTest.RenameTest;

public class RenameDeviceCommandValidatorTest
{
    private readonly RenameDeviceCommandValidator _sut = new();

    private static RenameDeviceCommand ValidCommand() => new()
    {
        UserId = Guid.NewGuid(),
        TargetDeviceId = Guid.NewGuid(),
        Name = "Work laptop",
    };

    [Theory]
    [InlineData("A")]
    [InlineData("abcdefghijklmnopqrstuvwxyz123456")]
    [InlineData("  abcdefghijklmnopqrstuvwxyz123456  ")]
    public void Validate_Given_Valid_Name_Should_Pass(string name)
    {
        _sut.Validate(ValidCommand() with { Name = name }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567")]
    public void Validate_Given_Invalid_Name_Should_Fail(string name)
    {
        _sut.Validate(ValidCommand() with { Name = name }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Empty_Ids_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { UserId = Guid.Empty }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { TargetDeviceId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
