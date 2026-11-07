using Cendrite.Message.Contracts.Messages.Device;
using Cendrite.Message.Contracts.Responses.Device;
using FakeItEasy;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Consumers;
using Identity.Application.Specifications;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.ConsumerTest;

public class ConnectionIdsConsumerTest
{
    [Fact]
    public async Task Consume_Should_Respond_With_Each_Users_Active_Connection_Ids()
    {
        // Arrange
        var alice = new User(Guid.NewGuid(), "key-a");
        var bob = new User(Guid.NewGuid(), "key-b");
        var aliceDevice = new Device(Guid.NewGuid(), "d-1", "1.0", DeviceType.Desktop, DevicePlatform.Linux, alice.Id);
        var bobDevice = new Device(Guid.NewGuid(), "d-2", "1.0", DeviceType.Desktop, DevicePlatform.Linux, bob.Id);
        alice.AddDevice(aliceDevice);
        bob.AddDevice(bobDevice);

        var repo = A.Fake<IUserRepository>();
        A.CallTo(() => repo.GetListAsync(A<UsersWithDevicesSpec>._, A<CancellationToken>._)).Returns([alice, bob]);
        var context = A.Fake<ConsumeContext<ConnectionIdsMessage>>();
        var message = new ConnectionIdsMessage { CorrelationId = Guid.NewGuid(), TenantIds = [alice.Id, bob.Id] };
        A.CallTo(() => context.Message).Returns(message);

        var sut = new ConnectionIdsConsumer(repo, A.Fake<ILogger<ConnectionIdsConsumer>>());

        // Act
        await sut.Consume(context);

        // Assert
        A.CallTo(() => context.RespondAsync(A<ConnectionIdsResponse>.That.Matches(r =>
                r.CorrelationId == message.CorrelationId
                && r.ConnectionIds!.Count == 2
                && r.ConnectionIds[alice.Id].SequenceEqual(new[] { aliceDevice.ConnectionId })
                && r.ConnectionIds[bob.Id].SequenceEqual(new[] { bobDevice.ConnectionId }))))
            .MustHaveHappenedOnceExactly();
    }
}
