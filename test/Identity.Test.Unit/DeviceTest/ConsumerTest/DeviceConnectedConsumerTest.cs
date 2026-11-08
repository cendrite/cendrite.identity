using AutoFixture;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Consumers;
using Cendrite.Message.Contracts.Messages.Client;
using FakeItEasy;
using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Cendrite.Authentication.Test.Unit.DeviceTest.ConsumerTest;

public class DeviceConnectedConsumerTest
{
    private readonly IUserRepository _fakeRepo;
    private readonly IUnitOfWork _fakeUow;
    private readonly DeviceConnectedConsumer _sut;
    private readonly Fixture _fixture = new();

    public DeviceConnectedConsumerTest()
    {
        _fakeRepo = A.Fake<IUserRepository>();
        _fakeUow = A.Fake<IUnitOfWork>();
        var logger = A.Fake<ILogger<DeviceConnectedConsumer>>();
        _sut = new DeviceConnectedConsumer(_fakeRepo, _fakeUow, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Message_Should_Not_Fail()
    {
        // Arrange
        var user = new User();
        user.AddDevice(new Device());
        var message = _fixture.Build<ClientConnectedMessage>()
            .With(x => x.DeviceId, user.Devices.First().Id)
            .Create();

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByDeviceIdWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        var fakeConsumeContext = A.Fake<ConsumeContext<ClientConnectedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        await _sut.Consume(fakeConsumeContext);

        // Assert
        user.Devices.First().LastSeen.Should().NotBeNull();
        A.CallTo(() => _fakeUow.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_User_Is_Null_Should_Throw_Exception()
    {
        // Arrange
        var message = _fixture.Create<ClientConnectedMessage>();

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByDeviceIdWithDevicesSpec>._, A<CancellationToken>._)).Returns<User?>(null);

        var fakeConsumeContext = A.Fake<ConsumeContext<ClientConnectedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        Func<Task> act = async () => await _sut.Consume(fakeConsumeContext);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

    }
}