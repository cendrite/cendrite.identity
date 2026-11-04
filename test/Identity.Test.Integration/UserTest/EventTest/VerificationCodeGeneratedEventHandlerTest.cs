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

public class VerificationCodeGeneratedEventHandlerTest : IntegrationTestBase
{
    private readonly VerificationCodeGeneratedEventHandler _sut;

    public VerificationCodeGeneratedEventHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailProducer = scope.ServiceProvider.GetRequiredService<IProducer<SendEmailMessage>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VerificationCodeGeneratedEventHandler>>();

        _sut = new VerificationCodeGeneratedEventHandler(userRepository, emailProducer, logger);
    }

    [Fact]
    public async Task Handle_Given_Valid_Email_Verification_Notification_Should_Send_Email()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("testPassword?923"), false);
        user.AddVerificationCode(UserVerificationCodeType.EmailVerification);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var notification = new VerificationCodeGeneratedEvent(user.Id, UserVerificationCodeType.EmailVerification);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        Harness.Published.Select<SendEmailMessage>().FirstOrDefault(uc => uc.Context.Message.To == user.Email!.Address)
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Given_Valid_Login_Verification_Notification_Should_Not_Send_Email()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("testPassword?923"), false);
        user.AddVerificationCode(UserVerificationCodeType.Login);

        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var notification = new VerificationCodeGeneratedEvent(user.Id, UserVerificationCodeType.Login);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        Harness.Published.Select<SendEmailMessage>().FirstOrDefault(uc => uc.Context.Message.To == user.Email!.Address)
            .Should().NotBeNull();
        Harness.Published.Select<SendEmailMessage>().FirstOrDefault(uc => uc.Context.Message.Body.Contains("Here is your login verification code: "))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Notification_Should_Throw_Exception()
    {
        // Arrange
        var notification = new VerificationCodeGeneratedEvent(Guid.NewGuid(), UserVerificationCodeType.EmailVerification);

        // Act
        var act = async () => await _sut.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}