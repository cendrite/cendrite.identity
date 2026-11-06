using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FluentAssertions;
using Identity.Application.Features.Device.Events;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.EventTest;

public class DeviceLinkRequestedEventHandlerTest : IntegrationTestBase
{
    private readonly DeviceLinkRequestedEventHandler _sut;

    public DeviceLinkRequestedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new DeviceLinkRequestedEventHandler(sp.GetRequiredService<IProducer<DeviceLinkRequestedMessage>>(),
            sp.GetRequiredService<ILogger<DeviceLinkRequestedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Event_Should_Publish_Message_For_The_Issuing_Device()
    {
        // Arrange
        var @event = new DeviceLinkRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-key", DevicePlatform.Linux);

        // Act
        await _sut.Handle(@event, CancellationToken.None);

        // Assert
        (await Harness.Published.Any<DeviceLinkRequestedMessage>(m =>
            m.Context.Message.PendingLinkId == @event.PendingLinkId && m.Context.Message.IssuingConnectionId == @event.IssuingConnectionId)).Should().BeTrue();
    }
}
