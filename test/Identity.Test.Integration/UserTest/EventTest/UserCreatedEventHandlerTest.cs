using Cendrite.Message.Contracts.Abstractions;
using Cendrite.Message.Contracts.Messages.Email;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Events;
using Identity.Domain.Aggregates;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.EventTest;

public class UserCreatedEventHandlerTest : IntegrationTestBase
{
    private readonly UserCreatedEventHandler _sut;
    public UserCreatedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailProducer = scope.ServiceProvider.GetRequiredService<IProducer<SendEmailMessage>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserCreatedEventHandler>>();
        _sut = new(userRepository, emailProducer, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Event_Should_Send_Email()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("validPassword=?23"), false);
        user.AddVerificationCode(UserVerificationCodeType.EmailVerification);
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var @event = new UserCreatedEvent(user.Id);

        // Act
        await _sut.Handle(@event, CancellationToken.None);

        // Assert
        Harness.Published.Select<SendEmailMessage>().FirstOrDefault(uc => uc.Context.Message.To == user.Email!.Address).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Event_Should_Throw_Exception()
    {
        // Arrange
        var @event = new UserCreatedEvent(Guid.NewGuid());

        // Act
        var act = async () => await _sut.Handle(@event, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}