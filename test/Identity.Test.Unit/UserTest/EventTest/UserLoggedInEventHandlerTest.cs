using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Features.User.Events;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.EventTest;

public class UserLoggedInEventHandlerTest
{
    private readonly IProducer<UserLoginMessage> _fakeProducer = A.Fake<IProducer<UserLoginMessage>>();
    private readonly UserLoggedInEventHandler _sut;
    private readonly UserLoggedInEvent _event = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    public UserLoggedInEventHandlerTest()
    {
        _sut = new UserLoggedInEventHandler(_fakeProducer, A.Fake<ILogger<UserLoggedInEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message()
    {
        // Act
        await _sut.Handle(_event, CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<UserLoginMessage>.That.Matches(m =>
                m.UserId == _event.UserId && m.DeviceId == _event.DeviceId && m.ConnectionId == _event.ConnectionId), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Publish_Fails_Should_Rethrow()
    {
        // Arrange
        A.CallTo(() => _fakeProducer.PublishMessageAsync(A<UserLoginMessage>._, A<CancellationToken>._)).Throws<InvalidOperationException>();

        // Act
        var act = async () => await _sut.Handle(_event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
