using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Rename;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.CommandTest.RenameTest;

public class RenameDeviceCommandHandlerTest : IntegrationTestBase
{
    private readonly RenameDeviceCommandHandler _sut;

    public RenameDeviceCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new RenameDeviceCommandHandler(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ILogger<RenameDeviceCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Own_Device_Should_Store_The_Name()
    {
        // Arrange
        var (user, device) = await SaveUserWithDevice();

        // Act
        var result = await _sut.Handle(Command(user, device), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        Db.Device.AsNoTracking().Single(d => d.Id == device.Id).Name.Should().Be("Work laptop");
    }

    [Fact]
    public async Task Handle_Given_Device_Of_Another_Account_Should_Return_NotFound()
    {
        // Arrange
        var (user, _) = await SaveUserWithDevice();
        var (_, otherDevice) = await SaveUserWithDevice();

        // Act
        var result = await _sut.Handle(Command(user, otherDevice), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(otherDevice.Id));
        Db.Device.AsNoTracking().Single(d => d.Id == otherDevice.Id).Name.Should().Be("PC.Linux");
    }

    [Fact]
    public async Task Handle_Given_Deleted_Device_Should_Return_NotFound()
    {
        // Arrange
        var (user, device) = await SaveUserWithDevice(deleted: true);

        // Act
        var result = await _sut.Handle(Command(user, device), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(device.Id));
    }

    private static RenameDeviceCommand Command(User user, Device device) => new()
    {
        UserId = user.Id,
        TargetDeviceId = device.Id,
        Name = " Work laptop ",
    };

    private async Task<(User, Device)> SaveUserWithDevice(bool deleted = false)
    {
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "testKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        if (deleted) device.SetDelete();
        user.AddDevice(device);
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();
        return (user, device);
    }
}
