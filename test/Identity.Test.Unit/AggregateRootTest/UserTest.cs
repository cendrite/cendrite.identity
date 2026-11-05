using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.AggregateRootTest;

public class UserTest
{
    [Fact]
    public void Constructor_Should_Set_AccountPublicKey()
    {
        // Act
        var user = new User(Guid.NewGuid(), "account-key");

        // Assert
        user.AccountPublicKey.Should().Be("account-key");
        user.Devices.Should().BeEmpty();
    }

    [Fact]
    public void GetDevices_Should_Exclude_Deleted_Devices_By_Default()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "account-key");
        var active = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        var deleted = new Device(Guid.NewGuid(), "key-2", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        deleted.SetDelete();
        user.AddDevice(active);
        user.AddDevice(deleted);

        // Act
        var activeDevices = user.GetDevices();
        var allDevices = user.GetDevices(excludeDeleted: false);

        // Assert
        activeDevices.Should().ContainSingle().Which.Should().Be(active);
        allDevices.Should().HaveCount(2);
    }

    [Fact]
    public void GetDevice_Should_Throw_For_Deleted_Device_Unless_Included()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "account-key");
        var device = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        device.SetDelete();
        user.AddDevice(device);

        // Act
        var act = () => user.GetDevice(device.Id);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        user.GetDevice(device.Id, includeDeleted: true).Should().Be(device);
    }
}
