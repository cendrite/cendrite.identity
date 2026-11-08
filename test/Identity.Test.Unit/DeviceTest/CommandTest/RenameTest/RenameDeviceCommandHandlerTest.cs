using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Rename;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.CommandTest.RenameTest;

public class RenameDeviceCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly RenameDeviceCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), "account-key");
    private readonly Device _device;

    public RenameDeviceCommandHandlerTest()
    {
        _sut = new RenameDeviceCommandHandler(_fakeRepo, _fakeUnitOfWork, A.Fake<ILogger<RenameDeviceCommandHandler>>());
        _device = new Device(Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_device);

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
    }

    private RenameDeviceCommand Command(Guid? deviceId = null) => new()
    {
        UserId = _user.Id,
        TargetDeviceId = deviceId ?? _device.Id,
        Name = "  Work laptop ",
    };

    [Fact]
    public async Task Handle_Given_Own_Device_Should_Rename_It_Trimmed()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _device.Name.Should().Be("Work laptop");
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Unknown_Device_Should_Return_NotFound()
    {
        // Arrange
        var otherDeviceId = Guid.NewGuid();

        // Act
        var result = await _sut.Handle(Command(otherDeviceId), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(otherDeviceId));
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Deleted_Device_Should_Return_NotFound()
    {
        // Arrange
        _device.SetDelete();

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_device.Id));
        _device.Name.Should().Be("PC.Linux");
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Return_NotFound()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_device.Id));
    }
}
