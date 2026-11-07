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

public class ConnectionIdConsumerTest : IntegrationTestBase
{
    private readonly ConnectionIdConsumer _sut;

    public ConnectionIdConsumerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new ConnectionIdConsumer(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<ILogger<ConnectionIdConsumer>>());
    }

    [Fact]
    public async Task Consume_Given_Stored_User_Should_Respond_With_Active_Connection_Ids_Only()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var active = new Device(Guid.NewGuid(), "key-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        var deleted = new Device(Guid.NewGuid(), "key-2", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        deleted.SetDelete();
        user.AddDevice(active);
        user.AddDevice(deleted);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var context = A.Fake<ConsumeContext<ConnectionIdMessage>>();
        A.CallTo(() => context.Message).Returns(new ConnectionIdMessage { CorrelationId = Guid.NewGuid(), TenantId = user.Id });

        // Act
        await _sut.Consume(context);

        // Assert
        A.CallTo(() => context.RespondAsync(A<ConnectionIdResponse>.That.Matches(r => r.ConnectionIds.SequenceEqual(new[] { active.ConnectionId }))))
            .MustHaveHappenedOnceExactly();
    }
}
