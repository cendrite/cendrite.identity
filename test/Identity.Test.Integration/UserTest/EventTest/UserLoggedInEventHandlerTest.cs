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

public class UserLoggedInEventHandlerTest : IntegrationTestBase
{
    private readonly UserLoggedInEventHandler _sut;

    public UserLoggedInEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new UserLoggedInEventHandler(sp.GetRequiredService<IProducer<UserLoginMessage>>(), sp.GetRequiredService<ILogger<UserLoggedInEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_User_Login_Message()
    {
        // Arrange
        var @event = new UserLoggedInEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        await _sut.Handle(@event, CancellationToken.None);

        // Assert
        (await Harness.Published.Any<UserLoginMessage>(m =>
            m.Context.Message.DeviceId == @event.DeviceId && m.Context.Message.ConnectionId == @event.ConnectionId)).Should().BeTrue();
    }
}
