using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Events;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.UserTest.EventTest;

public class UserLoggedOutEventHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IProducer<UserLogoutMessage> _fakeProducer = A.Fake<IProducer<UserLogoutMessage>>();
    private readonly UserLoggedOutEventHandler _sut;

    public UserLoggedOutEventHandlerTest()
    {
        _sut = new UserLoggedOutEventHandler(_fakeRepo, _fakeProducer, A.Fake<ILogger<UserLoggedOutEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Device_Should_Publish_Its_Connection_Id()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "account-key", UserTags.New());
        var device = new Device(Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        await _sut.Handle(new UserLoggedOutEvent(user.Id, device.Id), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<UserLogoutMessage>.That.Matches(m =>
                m.UserId == user.Id && m.DeviceId == device.Id && m.ConnectionId == device.ConnectionId), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Throw_Without_Publishing()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var act = async () => await _sut.Handle(new UserLoggedOutEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<UserLogoutMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
