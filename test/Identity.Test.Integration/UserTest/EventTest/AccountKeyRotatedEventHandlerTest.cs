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

public class AccountKeyRotatedEventHandlerTest : IntegrationTestBase
{
    private readonly AccountKeyRotatedEventHandler _sut;

    public AccountKeyRotatedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new AccountKeyRotatedEventHandler(sp.GetRequiredService<IProducer<AccountKeyRotatedMessage>>(),
            sp.GetRequiredService<ILogger<AccountKeyRotatedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Account_Key_Rotated_Message()
    {
        // Arrange
        var @event = new AccountKeyRotatedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Work laptop");

        // Act
        await _sut.Handle(@event, CancellationToken.None);

        // Assert
        (await Harness.Published.Any<AccountKeyRotatedMessage>(m => m.Context.Message.DeviceId == @event.DeviceId)).Should().BeTrue();
    }
}
