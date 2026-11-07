using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Features.User.Events;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.EventTest;

public class AccountKeyRotatedEventHandlerTest
{
    private readonly IProducer<AccountKeyRotatedMessage> _fakeProducer = A.Fake<IProducer<AccountKeyRotatedMessage>>();
    private readonly AccountKeyRotatedEventHandler _sut;
    private readonly AccountKeyRotatedEvent _event = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Work laptop");

    public AccountKeyRotatedEventHandlerTest()
    {
        _sut = new AccountKeyRotatedEventHandler(_fakeProducer, A.Fake<ILogger<AccountKeyRotatedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message()
    {
        // Act
        await _sut.Handle(_event, CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<AccountKeyRotatedMessage>.That.Matches(m =>
                m.UserId == _event.UserId
                && m.DeviceId == _event.DeviceId
                && m.ConnectionId == _event.ConnectionId
                && m.DeviceName == "Work laptop"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Publish_Fails_Should_Rethrow()
    {
        // Arrange
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<AccountKeyRotatedMessage>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

        // Act
        var act = async () => await _sut.Handle(_event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
