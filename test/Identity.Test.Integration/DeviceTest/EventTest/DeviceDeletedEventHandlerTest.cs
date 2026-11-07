using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Device;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Events;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.EventTest;

public class DeviceDeletedEventHandlerTest : IntegrationTestBase
{
    private readonly DeviceDeletedEventHandler _sut;

    public DeviceDeletedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new DeviceDeletedEventHandler(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IProducer<DeviceDeletedMessage>>(),
            sp.GetRequiredService<ILogger<DeviceDeletedEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Stored_Deleted_Device_Should_Publish_Device_Deleted_Message()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        device.SetDelete();
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        await _sut.Handle(new DeviceDeletedEvent(user.Id, device.Id), CancellationToken.None);

        // Assert
        (await Harness.Published.Any<DeviceDeletedMessage>(m => m.Context.Message.DeviceId == device.Id)).Should().BeTrue();
    }
}
