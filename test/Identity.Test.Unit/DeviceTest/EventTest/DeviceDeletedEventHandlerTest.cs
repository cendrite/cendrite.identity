using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Events;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.DeviceTest.EventTest;

public class DeviceDeletedEventHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IProducer<DeviceDeletedMessage> _fakeProducer = A.Fake<IProducer<DeviceDeletedMessage>>();
    private readonly DeviceDeletedEventHandler _sut;

    public DeviceDeletedEventHandlerTest()
    {
        _sut = new DeviceDeletedEventHandler(_fakeRepo, _fakeProducer, A.Fake<ILogger<DeviceDeletedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Deleted_Device_Should_Publish_Message()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "account-key", UserTags.New());
        var device = new Device(Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        device.SetDelete();
        user.AddDevice(device);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        await _sut.Handle(new DeviceDeletedEvent(user.Id, device.Id), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceDeletedMessage>.That.Matches(m => m.DeviceId == device.Id), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Throw_Without_Publishing()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var act = async () => await _sut.Handle(new DeviceDeletedEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceDeletedMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
