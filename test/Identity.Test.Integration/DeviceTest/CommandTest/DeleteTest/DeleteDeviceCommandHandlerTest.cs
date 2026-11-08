using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Delete;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.CommandTest.DeleteTest;

public class DeleteDeviceCommandHandlerTest : IntegrationTestBase
{
    private readonly DeleteDeviceCommandHandler _sut;

    public DeleteDeviceCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeleteDeviceCommandHandler>>();

        _sut = new(repo, authService, logger, unitOfWork);
    }

    [Fact]
    public async Task Handle_Given_Valid_Command_Should_Delete_Device_And_Raise_Logout_And_DeviceDeleted()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "testKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var command = new DeleteDeviceCommand
        {
            UserId = user.Id,
            TargetDeviceId = device.Id,
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        Db.Device.AsNoTracking().Single(d => d.Id == device.Id).Deleted.Should().NotBeNull();
        Db.OutboxMessage.Select(o => o.Type).AsEnumerable().Should().HaveCount(2)
            .And.Contain(t => t.StartsWith(typeof(UserLoggedOutEvent).FullName!))
            .And.Contain(t => t.StartsWith(typeof(DeviceDeletedEvent).FullName!));
    }

    [Fact]
    public async Task Handle_Given_Already_Deleted_Device_Should_Return_NotFound()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "testKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        device.SetDelete();
        user.AddDevice(device);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var command = new DeleteDeviceCommand
        {
            UserId = user.Id,
            TargetDeviceId = device.Id,
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(device.Id));
        Db.OutboxMessage.Any().Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Given_Invalid_UserId_Should_Return_NotFound()
    {
        // Arrange
        var command = new DeleteDeviceCommand
        {
            UserId = Guid.NewGuid(),
            TargetDeviceId = Guid.NewGuid(),
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(command.UserId));
    }
}
