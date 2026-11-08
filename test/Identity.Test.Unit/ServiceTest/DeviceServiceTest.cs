
using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Enums;
using Identity.Domain.Services.User;
using Identity.Test.Unit.Setup.Helpers;

namespace Cendrite.Authentication.Test.Unit.ServiceTest;

public class DeviceServiceTest
{
    private readonly IDeviceService _sut = new DeviceService();

    [Fact]
    public void RegisterDevice_Should_Return_Device()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());

        // Act
        var result = _sut.RegisterDevice(user, "publicKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0.0", DeviceType.Mobile, DevicePlatform.iOS, null, null);

        // Assert
        result.Should().NotBeNull();
        user.Devices.Should().HaveCount(1);
    }
}