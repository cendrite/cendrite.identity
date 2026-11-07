using FluentAssertions;
using Identity.Application.Features.Device.Commands.Delete;

namespace Identity.Test.Unit.DeviceTest.CommandTest.DeleteTest;

public class DeleteDeviceCommandValidatorTest
{
    private readonly DeleteDeviceCommandValidator _sut = new();

    [Fact]
    public void Validate_Given_Both_Ids_Should_Pass()
    {
        _sut.Validate(new DeleteDeviceCommand { UserId = Guid.NewGuid(), TargetDeviceId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Empty_Ids_Should_Fail()
    {
        _sut.Validate(new DeleteDeviceCommand { UserId = Guid.Empty, TargetDeviceId = Guid.NewGuid() }).IsValid.Should().BeFalse();
        _sut.Validate(new DeleteDeviceCommand { UserId = Guid.NewGuid(), TargetDeviceId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
