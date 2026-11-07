using Cendrite.Message.Contracts.Messages.Device;
using Cendrite.Message.Contracts.Responses.Device;
using FakeItEasy;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Consumers;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.ConsumerTest;

public class ConnectionIdsConsumerTest : IntegrationTestBase
{
    private readonly ConnectionIdsConsumer _sut;

    public ConnectionIdsConsumerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new ConnectionIdsConsumer(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<ILogger<ConnectionIdsConsumer>>());
    }

    [Fact]
    public async Task Consume_Given_Stored_Users_Should_Respond_With_Each_Users_Active_Connection_Ids()
    {
        // Arrange
        var alice = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var bob = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var aliceDevice = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, alice.Id);
        var bobDevice = new Device(Guid.NewGuid(), "key-2", "1.0", DeviceType.Desktop, DevicePlatform.Linux, bob.Id);
        var bobDeleted = new Device(Guid.NewGuid(), "key-3", "1.0", DeviceType.Desktop, DevicePlatform.Linux, bob.Id);
        bobDeleted.SetDelete();
        alice.AddDevice(aliceDevice);
        bob.AddDevice(bobDevice);
        bob.AddDevice(bobDeleted);
        Db.User.AddRange(alice, bob);
        await Db.SaveChangesAsync();

        var context = A.Fake<ConsumeContext<ConnectionIdsMessage>>();
        A.CallTo(() => context.Message).Returns(new ConnectionIdsMessage { CorrelationId = Guid.NewGuid(), TenantIds = [alice.Id, bob.Id] });

        // Act
        await _sut.Consume(context);

        // Assert
        A.CallTo(() => context.RespondAsync(A<ConnectionIdsResponse>.That.Matches(r =>
                r.ConnectionIds!.Count == 2
                && r.ConnectionIds[alice.Id].SequenceEqual(new[] { aliceDevice.ConnectionId })
                && r.ConnectionIds[bob.Id].SequenceEqual(new[] { bobDevice.ConnectionId }))))
            .MustHaveHappenedOnceExactly();
    }
}
