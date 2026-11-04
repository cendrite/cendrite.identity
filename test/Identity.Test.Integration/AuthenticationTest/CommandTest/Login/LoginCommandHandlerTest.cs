using FluentAssertions;
using Identity.Application.Caching.LoginNonce;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.Login;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Services.User;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.AuthenticationTest.CommandTest.Login;

public class LoginCommandHandlerTest : IntegrationTestBase
{
    private readonly LoginCommandHandler _sut;
    private readonly ILoginNonceCache _loginNonceCache;

    public LoginCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope().ServiceProvider;
        var repo = scope.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.GetRequiredService<IUnitOfWork>();
        var authservice = scope.GetRequiredService<IAuthenticationService>();
        var rateLimiter = scope.GetRequiredService<IRateLimiter>();
        _loginNonceCache = scope.GetRequiredService<ILoginNonceCache>();
        _sut = new LoginCommandHandler(repo, authservice, unitOfWork, rateLimiter, _loginNonceCache);
    }

    [Fact]
    public async Task Handle_Given_Valid_Login_Command_Should_Generate_Token_And_Return_LoginDto()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), true);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = "TestPassword?123",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        Db.VerificationCode.Should().HaveCount(1);
        result.Value.IsVerified.Should().BeTrue();
        result.Value.UserId.Should().NotBeEmpty().And.Be(user.Id);

        var nonce = await _loginNonceCache.GetNonceAsync(result.Value.NonceId!.Value, CancellationToken.None);
        nonce.Should().NotBeNull();
        nonce!.Stage.Should().Be(LoginNonceStage.PasswordVerified);
        nonce.IsValidFor(user.Id, result.Value.Nonce!, LoginNonceStage.PasswordVerified).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Given_Deleted_User_Should_Return_Invalid_Credentials()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("deleted.login@mail.dk"), Password.Create("TestPassword?123"), true);
        user.SetDelete();

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = "TestPassword?123",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        Db.VerificationCode.Should().HaveCount(0);
    }

    [Fact]
    public async Task Handle_Command_Given_Invalid_Email_Should_Return_Result_Fail()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), true);

        const string invalidEmail = "this will be invalid";
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = invalidEmail,
            Password = "TestPassword?123",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Command_Given_Invalid_Password_Should_Return_Result_Fail()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), true);

        const string invalidPw = "this will be invalid";
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = invalidPw,
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Command_Given_User_Not_Verified_Should_Return_Result_Fail()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), false); // Unverified user

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = "TestPassword?123",
        };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Given_Too_Many_Failed_Passwords_Should_Return_TooManyRequests_Even_For_Correct_Password()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@mail.dk";
        var user = new User(Guid.NewGuid(), Email.Create(email), Password.Create("TestPassword?123"), true);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var wrongPassword = new LoginCommand { Email = email, Password = "this will be invalid" };
        for (var i = 0; i < 5; i++)
        {
            var failed = await _sut.Handle(wrongPassword, CancellationToken.None);
            failed.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        }

        // Act
        var result = await _sut.Handle(new LoginCommand { Email = email, Password = "TestPassword?123" }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        Db.VerificationCode.Should().HaveCount(0);
    }

    [Fact]
    public async Task Handle_Given_Correct_Password_Should_Reset_Failed_Attempts()
    {
        // Arrange
        var email = $"{Guid.NewGuid():N}@mail.dk";
        var user = new User(Guid.NewGuid(), Email.Create(email), Password.Create("TestPassword?123"), true);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var wrongPassword = new LoginCommand { Email = email, Password = "this will be invalid" };
        var correctPassword = new LoginCommand { Email = email, Password = "TestPassword?123" };

        for (var i = 0; i < 4; i++)
            await _sut.Handle(wrongPassword, CancellationToken.None);

        (await _sut.Handle(correctPassword, CancellationToken.None)).Success.Should().BeTrue();

        for (var i = 0; i < 4; i++)
            await _sut.Handle(wrongPassword, CancellationToken.None);

        // Act
        var result = await _sut.Handle(correctPassword, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }
}
