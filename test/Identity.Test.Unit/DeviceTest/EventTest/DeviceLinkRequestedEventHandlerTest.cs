using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Features.Device.Events;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.EventTest;

public class DeviceLinkRequestedEventHandlerTest
{
    private readonly IProducer<DeviceLinkRequestedMessage> _fakeProducer = A.Fake<IProducer<DeviceLinkRequestedMessage>>();
    private readonly DeviceLinkRequestedEventHandler _sut;
    private readonly DeviceLinkRequestedEvent _event = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-key", DevicePlatform.Linux);

    public DeviceLinkRequestedEventHandlerTest()
    {
        _sut = new DeviceLinkRequestedEventHandler(_fakeProducer, A.Fake<ILogger<DeviceLinkRequestedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message()
    {
        // Act
        await _sut.Handle(_event, CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceLinkRequestedMessage>.That.Matches(m =>
                m.UserId == _event.UserId
                && m.IssuingDeviceId == _event.IssuingDeviceId
                && m.IssuingConnectionId == _event.IssuingConnectionId
                && m.PendingLinkId == _event.PendingLinkId
                && m.DevicePublicKey == "device-key"
                && m.DevicePlatform == "Linux"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Publish_Fails_Should_Rethrow()
    {
        // Arrange
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<DeviceLinkRequestedMessage>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

        // Act
        var act = async () => await _sut.Handle(_event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
