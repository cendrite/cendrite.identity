using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Delete;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.CommandTest.DeleteTest;

public class DeleteDeviceCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IAuthenticationService _fakeAuthenticationService = A.Fake<IAuthenticationService>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly DeleteDeviceCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), "account-key");
    private readonly Device _device;

    public DeleteDeviceCommandHandlerTest()
    {
        _sut = new DeleteDeviceCommandHandler(_fakeRepo, _fakeAuthenticationService, A.Fake<ILogger<DeleteDeviceCommandHandler>>(), _fakeUnitOfWork);

        _device = new Device(Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_device);

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
    }

    [Fact]
    public async Task Handle_Given_Own_Device_Should_Log_It_Out_And_Raise_Event()
    {
        // Act
        var result = await _sut.Handle(new DeleteDeviceCommand { UserId = _user.Id, TargetDeviceId = _device.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeAuthenticationService.Logout(_user, _device.Id)).MustHaveHappenedOnceExactly();
        _user.DomainEvents.Should().ContainSingle(e => e is DeviceDeletedEvent && ((DeviceDeletedEvent)e).DeviceId == _device.Id);
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Unknown_Device_Should_Return_NotFound()
    {
        // Arrange
        var deviceId = Guid.NewGuid();

        // Act
        var result = await _sut.Handle(new DeleteDeviceCommand { UserId = _user.Id, TargetDeviceId = deviceId }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(deviceId));
        A.CallTo(() => _fakeAuthenticationService.Logout(A<User>._, A<Guid>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Return_NotFound()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new DeleteDeviceCommand { UserId = _user.Id, TargetDeviceId = _device.Id }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_user.Id));
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
