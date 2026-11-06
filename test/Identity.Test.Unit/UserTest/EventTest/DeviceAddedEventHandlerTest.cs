using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Features.User.Events;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.EventTest;

public class DeviceAddedEventHandlerTest
{
    private readonly IProducer<DeviceAddedMessage> _fakeProducer = A.Fake<IProducer<DeviceAddedMessage>>();
    private readonly DeviceAddedEventHandler _sut;
    private readonly DeviceAddedEvent _event = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DevicePlatform.Windows);

    public DeviceAddedEventHandlerTest()
    {
        _sut = new DeviceAddedEventHandler(_fakeProducer, A.Fake<ILogger<DeviceAddedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message()
    {
        // Act
        await _sut.Handle(_event, CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceAddedMessage>.That.Matches(m =>
                m.UserId == _event.UserId
                && m.DeviceId == _event.DeviceId
                && m.ConnectionId == _event.ConnectionId
                && m.DevicePlatform == "Windows"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Publish_Fails_Should_Rethrow()
    {
        // Arrange
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceAddedMessage>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

        // Act
        var act = async () => await _sut.Handle(_event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
