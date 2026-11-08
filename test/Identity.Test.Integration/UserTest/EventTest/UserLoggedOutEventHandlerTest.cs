using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.User;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Events;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Identity.Test.Integration.Setup.Helpers;

namespace Identity.Test.Integration.UserTest.EventTest;

public class UserLoggedOutEventHandlerTest : IntegrationTestBase
{
    private readonly UserLoggedOutEventHandler _sut;

    public UserLoggedOutEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new UserLoggedOutEventHandler(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IProducer<UserLogoutMessage>>(),
            sp.GetRequiredService<ILogger<UserLoggedOutEventHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Stored_Device_Should_Publish_Its_Connection_Id()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), "key-1", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        await _sut.Handle(new UserLoggedOutEvent(user.Id, device.Id), CancellationToken.None);

        // Assert
        (await Harness.Published.Any<UserLogoutMessage>(m =>
            m.Context.Message.DeviceId == device.Id && m.Context.Message.ConnectionId == device.ConnectionId)).Should().BeTrue();
    }
}
