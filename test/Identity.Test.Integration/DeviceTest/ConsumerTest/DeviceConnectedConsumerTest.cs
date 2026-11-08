using AutoFixture;
using Cendrite.Message.Contracts.Messages.Client;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Consumers;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Identity.Test.Integration.Setup.Helpers;

namespace Identity.Test.Integration.DeviceTest.ConsumerTest;

public class DeviceConnectedConsumerTest : IntegrationTestBase
{
    private readonly DeviceConnectedConsumer _sut;
    public DeviceConnectedConsumerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeviceConnectedConsumer>>();
        _sut = new DeviceConnectedConsumer(repo, unitOfWork, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Context_Should_Update_Device_LastSeen()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());

        var device = new Device(Guid.NewGuid(), "test", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        user.AddDevice(device);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var message = Fixture.Build<ClientConnectedMessage>()
            .With(x => x.DeviceId, device.Id).Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<ClientConnectedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        await _sut.Consume(fakeConsumeContext);

        // Assert
        Db.Device.AsNoTracking().First().LastSeen.Should().BeCloseTo(DateTime.Now, new TimeSpan(0, 3, 0, 0));
    }

    [Fact]
    public async Task Handle_Given_Throws_Exception_Should_Not_Update_Device()
    {
        //Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());

        var device = new Device(Guid.NewGuid(), "test", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        device.SetDelete(); // Set device to deleted to throw exception

        user.AddDevice(device);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var message = Fixture.Build<ClientConnectedMessage>()
            .With(x => x.DeviceId, device.Id).Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<ClientConnectedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        var act = async () => await _sut.Consume(fakeConsumeContext);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        Db.Device.AsNoTracking().First().LastSeen.Should().BeNull();
    }
}