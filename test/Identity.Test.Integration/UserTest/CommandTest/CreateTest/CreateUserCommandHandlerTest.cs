using Cendrite.Message.Contracts.Messages.Profile;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Commands.Create;
using Identity.Domain.Aggregates;
using Identity.Domain.Services.User;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.CommandTest.CreateTest;

public class CreateUserCommandHandlerTest : IntegrationTestBase
{
    private readonly CreateUserCommandHandler _sut;

    public CreateUserCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {

        var scope = factory.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserLifeCycleService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var requestClient = scope.ServiceProvider.GetRequiredService<IRequestClient<CreateUserProfileMessage>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CreateUserCommandHandler>>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        _sut = new CreateUserCommandHandler(userRepository, userService, unitOfWork, requestClient, logger);
    }

    [Fact]
    public async Task Handle_WhenEmailExists_ReturnsFailure()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("hello@test.dk"), Password.Create("validPassword=?23"), false);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var command = new CreateUserCommand()
        {
            Email = user.Email!.Address,
            Password = "validPassword=?23",
            Username = "validUsername"
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Description.Should().Be("An account already exists with that email");
        Db.User.Should().HaveCount(1);
        Db.Device.Should().HaveCount(0);
        Db.OutboxMessage.Should().HaveCount(0);
    }

    [Fact]
    public async Task Handle_WhenUserCreationFails_ReturnsFailure()
    {
        // Arrange
        var command = new CreateUserCommand()
        {
            Email = "wrong email",
            Password = "wrong password",
            Username = "validUsername",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Description.Should().Be("Invalid email address.");
        Db.User.Should().HaveCount(0);
        Db.Device.Should().HaveCount(0);
        Db.OutboxMessage.Should().HaveCount(0);
    }

    [Fact]
    public async Task Handle_WhenUserCreationSucceeds_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateUserCommand()
        {
            Email = "valid@email.dk",
            Password = "validPassword=?23",
            Username = "validUsername",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Harness.Published.Select<CreateUserProfileMessage>().Where(cr => cr.Context.Message.Username == "validUsername").Should().HaveCount(1);
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        Db.User.Should().HaveCount(1);
        Db.OutboxMessage.Count().Should().Be(1);
    }
}
