using Identity.Application.Caching.LoginNonce;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Create;
using Cendrite.Message.Contracts.Messages.Client;
using FakeItEasy;
using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Enums;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Cendrite.Authentication.Test.Unit.DeviceTest.CreateTest;

public class CreateDeviceCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo;
    private readonly ILoginNonceCache _fakeLoginNonceCache;
    private readonly IDeviceService _fakeDeviceService;
    private readonly IUnitOfWork _fakeUnitOfWork;
    private readonly IRequestClient<CreateClientMessage> _fakeRequestClient;
    private readonly CreateDeviceCommandHandler _sut;

    public CreateDeviceCommandHandlerTest()
    {
        _fakeRepo = A.Fake<IUserRepository>();
        _fakeLoginNonceCache = A.Fake<ILoginNonceCache>();
        _fakeDeviceService = A.Fake<IDeviceService>();
        _fakeUnitOfWork = A.Fake<IUnitOfWork>();
        _fakeRequestClient = A.Fake<IRequestClient<CreateClientMessage>>();

        _sut = new CreateDeviceCommandHandler(_fakeRepo, _fakeRequestClient, _fakeLoginNonceCache, _fakeDeviceService, _fakeUnitOfWork, A.Fake<ILogger<CreateDeviceCommandHandler>>());
    }


    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateDeviceCommand
        {
            UserId = Guid.NewGuid(),
            LoginNonceId = Guid.Empty,
            LoginNonce = null,
            DeviceAppVersion = null,
            DeviceType = 0,
            DevicePlatform = 0,
            Base64DevicePublicKey = null
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(request.UserId));
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(A<Guid>._, CancellationToken.None)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WhenLoginNonceNotFound_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateDeviceCommand
        {
            UserId = Guid.NewGuid(),
            LoginNonceId = Guid.NewGuid(),
            LoginNonce = "nonce",
            DeviceAppVersion = null,
            DeviceType = 0,
            DevicePlatform = 0,
            Base64DevicePublicKey = null
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(new User());
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns((LoginNonce)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WhenLoginNonceInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateDeviceCommand
        {
            UserId = Guid.NewGuid(),
            LoginNonceId = Guid.NewGuid(),
            LoginNonce = "nonce",
            DeviceAppVersion = null,
            DeviceType = 0,
            DevicePlatform = 0,
            Base64DevicePublicKey = null
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(new User());
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns(LoginNonce.Create(Guid.NewGuid(), LoginNonceStage.CodeVerified));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WhenLoginNonceBelongsToAnotherUser_ReturnsUnauthorized()
    {
        // Arrange
        var victimId = Guid.NewGuid();
        var attackerNonce = LoginNonce.Create(Guid.NewGuid(), LoginNonceStage.CodeVerified);

        var request = new CreateDeviceCommand
        {
            UserId = victimId,
            LoginNonceId = attackerNonce.Id,
            LoginNonce = attackerNonce.NonceValue,
            DeviceAppVersion = "1.0",
            DeviceType = DeviceType.Desktop,
            DevicePlatform = DevicePlatform.Linux,
            Base64DevicePublicKey = "key"
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(new User());
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns(attackerNonce);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeLoginNonceCache.TryClaimNonceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WhenLoginNonceAlreadyClaimed_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var loginNonce = LoginNonce.Create(userId, LoginNonceStage.CodeVerified);

        var request = new CreateDeviceCommand
        {
            UserId = userId,
            LoginNonceId = loginNonce.Id,
            LoginNonce = loginNonce.NonceValue,
            DeviceAppVersion = "1.0",
            DeviceType = DeviceType.Desktop,
            DevicePlatform = DevicePlatform.Linux,
            Base64DevicePublicKey = "key"
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(new User());
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns(loginNonce);
        A.CallTo(() => _fakeLoginNonceCache.TryClaimNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns(false);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_WhenLoginNonceOnlyPasswordVerified_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var passwordNonce = LoginNonce.Create(userId, LoginNonceStage.PasswordVerified);

        var request = new CreateDeviceCommand
        {
            UserId = userId,
            LoginNonceId = passwordNonce.Id,
            LoginNonce = passwordNonce.NonceValue,
            DeviceAppVersion = "1.0",
            DeviceType = DeviceType.Desktop,
            DevicePlatform = DevicePlatform.Linux,
            Base64DevicePublicKey = "key"
        };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(new User());
        A.CallTo(() => _fakeLoginNonceCache.GetNonceAsync(request.LoginNonceId, CancellationToken.None)).Returns(passwordNonce);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeLoginNonceCache.TryClaimNonceAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._)).MustNotHaveHappened();
    }
}
