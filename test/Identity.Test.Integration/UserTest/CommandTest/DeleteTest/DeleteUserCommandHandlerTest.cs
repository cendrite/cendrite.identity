using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Commands.Delete;
using Identity.Domain.Aggregates;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Identity.Test.Integration.Setup.Helpers;

namespace Identity.Test.Integration.UserTest.CommandTest.DeleteTest;

public class DeleteUserCommandHandlerTest : IntegrationTestBase
{
    private readonly DeleteUserCommandHandler _sut;

    public DeleteUserCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var userLifeCycleServices = scope.ServiceProvider.GetRequiredService<IUserLifeCycleService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeleteUserCommandHandler>>();

        _sut = new DeleteUserCommandHandler(repo, unitOfWork, userLifeCycleServices, logger);
    }

    [Fact]
    public async Task Handle_When_Command_Is_Valid_Should_Delete_User()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        var cmd = new DeleteUserCommand()
        {
            Id = user.Id
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var deletedUser = await Db.User.AsNoTracking().FirstOrDefaultAsync(x => x.Id == user.Id);
        deletedUser.Deleted.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_When_User_Is_Not_Found_Should_Return_NotFound()
    {
        // Arrange
        var cmd = new DeleteUserCommand()
        {
            Id = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Description.Should().Contain("Could not find entity with ID");
    }

    [Fact]
    public async Task Handle_When_User_Is_Already_Deleted_Should_Return_UnspecifiedError()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        user.SetDelete();
        await Db.AddAsync(user);
        await Db.SaveChangesAsync();

        var cmd = new DeleteUserCommand()
        {
            Id = user.Id
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Description.Should().Contain("User is already marked as deleted");
    }
}
