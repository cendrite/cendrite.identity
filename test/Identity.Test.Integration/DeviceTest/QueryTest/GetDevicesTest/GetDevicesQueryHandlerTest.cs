using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetDevices;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.QueryTest.GetDevicesTest;

public class GetDevicesQueryHandlerTest : IntegrationTestBase
{
    private readonly GetDevicesQueryHandler _sut;
    public GetDevicesQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var repo = serviceProvider.GetRequiredService<IUserRepository>();
        var logger = serviceProvider.GetRequiredService<ILogger<GetDevicesQueryHandler>>();

        _sut = new GetDevicesQueryHandler(repo, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Query_Should_Return_Devices()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var device1 = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        var device2 = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        user.AddDevice(device1);
        user.AddDevice(device2);

        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        var query = new GetDevicesQuery()
        {
            UserId = user.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Devices.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Given_Valid_Query_Should_Return_LastSeen_And_Exclude_Deleted_Devices()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var seenDevice = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        seenDevice.SetLastSeen();

        var neverSeenDevice = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        var deletedDevice = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        deletedDevice.SetDelete();

        user.AddDevice(seenDevice);
        user.AddDevice(neverSeenDevice);
        user.AddDevice(deletedDevice);

        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(new GetDevicesQuery { UserId = user.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Devices.Should().HaveCount(2);
        result.Value.Devices.Single(d => d.Id == seenDevice.Id).LastSeen.Should().NotBeNull();
        result.Value.Devices.Single(d => d.Id == neverSeenDevice.Id).LastSeen.Should().BeNull();
        result.Value.Devices.Should().NotContain(d => d.Id == deletedDevice.Id);
    }

    [Fact]
    public async Task Handle_Given_Invalid_Query_Should_Return_Error()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var device1 = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        var device2 = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        user.AddDevice(device1);
        user.AddDevice(device2);

        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        var query = new GetDevicesQuery()
        {
            UserId = Guid.NewGuid() // will be invalid
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(query.UserId));
    }

    [Fact]
    public async Task Handle_Valid_Query_With_No_Devices_Should_Return_Empty_List()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var device = new Device(Guid.NewGuid(), "somekey", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        device.SetDelete();

        user.AddDevice(device);
        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        var query = new GetDevicesQuery()
        {
            UserId = user.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);


        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Devices.Count.Should().Be(0);
    }
}