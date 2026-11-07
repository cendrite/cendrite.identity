using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using Identity.Application.Features.User.Events;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.EventTest;

public class UserDeletedEventHandlerTest
{
    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message()
    {
        // Arrange
        var producer = A.Fake<IProducer<UserDeletedMessage>>();
        var sut = new UserDeletedEventHandler(producer, A.Fake<ILogger<UserDeletedEventHandler>>());
        var userId = Guid.NewGuid();

        // Act
        await sut.Handle(new UserDeletedEvent(userId), CancellationToken.None);

        // Assert
        A.CallTo(() => producer.PublishMessageAsync(A<UserDeletedMessage>.That.Matches(m => m.UserId == userId), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
