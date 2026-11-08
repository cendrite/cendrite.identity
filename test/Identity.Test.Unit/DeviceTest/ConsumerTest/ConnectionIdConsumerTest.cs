using Cendrite.Message.Contracts.Messages.Device;
using Cendrite.Message.Contracts.Responses.Device;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Consumers;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.DeviceTest.ConsumerTest;

public class ConnectionIdConsumerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly ConsumeContext<ConnectionIdMessage> _fakeContext = A.Fake<ConsumeContext<ConnectionIdMessage>>();
    private readonly ConnectionIdConsumer _sut;
    private readonly ConnectionIdMessage _message = new() { CorrelationId = Guid.NewGuid(), TenantId = Guid.NewGuid() };

    public ConnectionIdConsumerTest()
    {
        _sut = new ConnectionIdConsumer(_fakeRepo, A.Fake<ILogger<ConnectionIdConsumer>>());
        A.CallTo(() => _fakeContext.Message).Returns(_message);
    }

    [Fact]
    public async Task Consume_Given_User_Should_Respond_With_Active_Connection_Ids_Only()
    {
        // Arrange
        var user = new User(_message.TenantId, "account-key", UserTags.New());
        var active = new Device(Guid.NewGuid(), "key-1", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        var deleted = new Device(Guid.NewGuid(), "key-2", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        deleted.SetDelete();
        user.AddDevice(active);
        user.AddDevice(deleted);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        await _sut.Consume(_fakeContext);

        // Assert
        A.CallTo(() => _fakeContext.RespondAsync(A<ConnectionIdResponse>.That.Matches(r =>
                r.IsSuccess && r.CorrelationId == _message.CorrelationId && r.ConnectionIds.SequenceEqual(new[] { active.ConnectionId }))))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Consume_Given_Unknown_User_Should_Throw_Without_Responding()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var act = async () => await _sut.Consume(_fakeContext);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        A.CallTo(() => _fakeContext.RespondAsync(A<ConnectionIdResponse>._)).MustNotHaveHappened();
    }
}
