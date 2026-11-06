using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FluentAssertions;
using Identity.Application.Features.User.Events;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.EventTest;

public class DeviceAddedEventHandlerTest : IntegrationTestBase
{
    private readonly DeviceAddedEventHandler _sut;

    public DeviceAddedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new DeviceAddedEventHandler(sp.GetRequiredService<IProducer<DeviceAddedMessage>>(), sp.GetRequiredService<ILogger<DeviceAddedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Device_Added_Message()
    {
        // Arrange
        var @event = new DeviceAddedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DevicePlatform.Linux);

        // Act
        await _sut.Handle(@event, CancellationToken.None);

        // Assert
        (await Harness.Published.Any<DeviceAddedMessage>(m => m.Context.Message.DeviceId == @event.DeviceId)).Should().BeTrue();
    }
}
