using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.Logout;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.AuthenticationTest.CommandTest.Logout;

public class LogoutCommandHandlerTest : IntegrationTestBase
{
    private readonly LogoutCommandHandler _sut;
    public LogoutCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LogoutCommandHandler>>();

        _sut = new(repo, authService, unitOfWork, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Command_Should_Logout_User_And_Delete_Device()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var device = new Device(Guid.NewGuid(), "testKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);

        user.AddDevice(device);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var command = new LogoutCommand
        {
            Id = user.Id,
            DeviceId = device.Id,
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        Db.Device.AsNoTracking().FirstOrDefault()!.Deleted.Should().NotBeNull();
        Db.OutboxMessage.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_Given_Invalid_UserId_Should_Return_Result_Fail()
    {
        // Arrange
        var command = new LogoutCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(command.Id));
    }
}
