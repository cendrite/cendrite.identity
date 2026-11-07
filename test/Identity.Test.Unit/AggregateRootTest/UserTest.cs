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

    [Fact]
    public void RotateAccountKey_Should_Record_Old_And_New_Key_And_Swap_The_Key()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "old-key");
        var device = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);

        // Act
        var rotation = user.RotateAccountKey(device.Id, "new-key", "proof", "approval");

        // Assert
        user.AccountPublicKey.Should().Be("new-key");
        user.AccountKeyRotations.Should().ContainSingle().Which.Should().Be(rotation);
        rotation.UserId.Should().Be(user.Id);
        rotation.DeviceId.Should().Be(device.Id);
        rotation.OldAccountPublicKey.Should().Be("old-key");
        rotation.NewAccountPublicKey.Should().Be("new-key");
        rotation.Proof.Should().Be("proof");
        rotation.Approval.Should().Be("approval");
    }

    [Fact]
    public void RotateAccountKey_Should_Chain_Consecutive_Rotations()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "key-a");
        var device = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);

        // Act
        user.RotateAccountKey(device.Id, "key-b", "proof-1", "approval-1");
        var second = user.RotateAccountKey(device.Id, "key-c", "proof-2", "approval-2");

        // Assert
        second.OldAccountPublicKey.Should().Be("key-b");
        user.AccountPublicKey.Should().Be("key-c");
        user.AccountKeyRotations.Should().HaveCount(2);
    }

    [Fact]
    public void RotateAccountKey_From_Deleted_Device_Should_Throw_And_Keep_The_Key()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "old-key");
        var device = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        device.SetDelete();
        user.AddDevice(device);

        // Act
        var act = () => user.RotateAccountKey(device.Id, "new-key", "proof", "approval");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        user.AccountPublicKey.Should().Be("old-key");
        user.AccountKeyRotations.Should().BeEmpty();
    }
}
