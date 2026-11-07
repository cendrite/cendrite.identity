using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FluentAssertions;
using Identity.Application.Features.User.Events;
using Identity.Domain.Events.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.EventTest;

public class UserDeletedEventHandlerTest : IntegrationTestBase
{
    private readonly UserDeletedEventHandler _sut;

    public UserDeletedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new UserDeletedEventHandler(sp.GetRequiredService<IProducer<UserDeletedMessage>>(), sp.GetRequiredService<ILogger<UserDeletedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_User_Deleted_Message()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _sut.Handle(new UserDeletedEvent(userId), CancellationToken.None);

        // Assert
        (await Harness.Published.Any<UserDeletedMessage>(m => m.Context.Message.UserId == userId)).Should().BeTrue();
    }
}
