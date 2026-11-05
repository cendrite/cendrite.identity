using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Dtos;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.AuthenticationTest.CommandTest.VerifyNonce;

public class VerifyNonceCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo;
    private readonly INonceCacheService _fakeCachce;
    private readonly IJwtService _fakeJwt;
    private readonly ITokenIssuer _fakeIssuer;
    private readonly IVerifyNonceService _fakeVerify;
    private readonly IUnitOfWork _fakeUnit;

    private readonly VerifyNonceCommandHandler _sut;

    public VerifyNonceCommandHandlerTest()
    {
        _fakeCachce = A.Fake<INonceCacheService>();
        _fakeJwt = A.Fake<IJwtService>();
        _fakeIssuer = A.Fake<ITokenIssuer>();
        _fakeRepo = A.Fake<IUserRepository>();
        _fakeVerify = A.Fake<IVerifyNonceService>();
        _fakeUnit = A.Fake<IUnitOfWork>();
        var fakeLogger = A.Fake<ILogger<VerifyNonceCommandHandler>>();

        _sut = new(_fakeRepo, _fakeCachce, _fakeJwt, _fakeIssuer, _fakeVerify, _fakeUnit, fakeLogger);
    }

    private static (User user, Device device) CreateUserWithDevice()
    {
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "doesntmatter", "1.0", DeviceType.Desktop,
            DevicePlatform.Windows, user.Id);
        user.AddDevice(device);
        return (user, device);
    }

    private static VerifyNonceCommand CreateCommand(Guid userId, Guid deviceId, Guid nonceId, bool announce = false) => new()
    {
        UserId = userId,
        DeviceId = deviceId,
        NonceId = nonceId,
        Nonce = "signature",
        Announce = announce,
    };

    [Fact]
    public async Task Handle_When_Nonce_Not_Found_Should_Return_Unauthorized()
    {
        // Arrange
        var cmd = CreateCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns((Nonce)null!);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_Nonce_Belongs_To_Another_User_Should_Return_Unauthorized()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(Guid.NewGuid(), device.Id);
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_Nonce_Belongs_To_Another_Device_Should_Return_Unauthorized()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(user.Id, Guid.NewGuid());
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_Nonce_Expired_Should_Return_Unauthorized()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.FromCache(Guid.NewGuid(), "value", user.Id, device.Id,
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5));
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_When_User_Not_Found_Should_Return_Unauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var nonce = Nonce.Create(userId, deviceId);
        var cmd = CreateCommand(userId, deviceId, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User)null!);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_When_Device_Deleted_Should_Return_Unauthorized()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        device.SetDelete();
        var nonce = Nonce.Create(user.Id, device.Id);
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_When_Signature_Invalid_Should_Return_Unauthorized_And_Not_Claim()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(user.Id, device.Id);
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _fakeVerify.VerifyNonce(Labelled(nonce.NonceValue), cmd.Nonce, device.PublicKey)).Returns(false);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_Nonce_Already_Claimed_Should_Return_Unauthorized_And_Not_Issue_Tokens()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(user.Id, device.Id);
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id);

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _fakeVerify.VerifyNonce(Labelled(nonce.NonceValue), cmd.Nonce, device.PublicKey)).Returns(true);
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeJwt.GenerateTokenAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnit.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_When_Nonce_Valid_Should_Return_Token_And_Announce_Only_When_Asked(bool announce)
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(user.Id, device.Id);
        var cmd = CreateCommand(user.Id, device.Id, nonce.Id, announce);
        var accessToken = new AccessToken("jwt", DateTime.UtcNow.AddMinutes(15));

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _fakeVerify.VerifyNonce(Labelled(nonce.NonceValue), cmd.Nonce, device.PublicKey)).Returns(true);
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeJwt.GenerateTokenAsync(user.Id, device.Id, device.ConnectionId, A<CancellationToken>._)).Returns(accessToken);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Jwt.Should().Be(accessToken.Token);
        result.Value.ExpiresAt.Should().Be(accessToken.ExpiresAt);
        user.DomainEvents.OfType<UserLoggedInEvent>().Should().HaveCount(announce ? 1 : 0);
        A.CallTo(() => _fakeUnit.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeCachce.DeleteNonceAsync(cmd.NonceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Nonce_Valid_Should_Return_Token_Responses_From_The_Issuer()
    {
        // Arrange
        var (user, device) = CreateUserWithDevice();
        var nonce = Nonce.Create(user.Id, device.Id);
        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = "signature",
            TokenRequests = ["request-1", "request-2"],
        };

        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(nonce);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _fakeVerify.VerifyNonce(Labelled(nonce.NonceValue), cmd.Nonce, device.PublicKey)).Returns(true);
        A.CallTo(() => _fakeCachce.TryClaimNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeJwt.GenerateTokenAsync(user.Id, device.Id, device.ConnectionId, A<CancellationToken>._))
            .Returns(new AccessToken("jwt", DateTime.UtcNow.AddMinutes(15)));
        A.CallTo(() => _fakeIssuer.IssueAsync(user.Id, cmd.TokenRequests, A<CancellationToken>._))
            .Returns(["response-1", "response-2"]);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Jwt.Should().Be("jwt");
        result.Value.TokenResponses.Should().Equal("response-1", "response-2");
    }

    [Fact]
    public async Task Handle_When_Nonce_Invalid_Should_Not_Issue_Tokens()
    {
        // Arrange
        var cmd = CreateCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        A.CallTo(() => _fakeCachce.GetNonceAsync(cmd.NonceId, A<CancellationToken>._)).Returns((Nonce)null!);

        // Act
        await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeIssuer.IssueAsync(A<Guid>._, A<IReadOnlyList<string>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static string Labelled(string nonce) =>
        Convert.ToBase64String([.. System.Text.Encoding.ASCII.GetBytes("cendrite-auth-v1"), .. Convert.FromBase64String(nonce)]);
}
