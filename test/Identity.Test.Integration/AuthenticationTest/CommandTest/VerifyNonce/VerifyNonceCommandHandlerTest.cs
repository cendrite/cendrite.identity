using FluentAssertions;
using Identity.Application.Caching;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Identity.Test.Integration.AuthenticationTest.CommandTest.VerifyNonce;

public class VerifyNonceCommandHandlerTest : IntegrationTestBase
{
    private readonly VerifyNonceCommandHandler _sut;
    public VerifyNonceCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var nonceCache = scope.ServiceProvider.GetRequiredService<INonceCacheService>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<ITokenIssuer>();
        var verifyNonceService = scope.ServiceProvider.GetRequiredService<IVerifyNonceService>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VerifyNonceCommandHandler>>();

        _sut = new VerifyNonceCommandHandler(repo, nonceCache, jwtService, tokenIssuer, verifyNonceService, uow, logger);
    }

    [Fact]
    public async Task Handle_Given_Command_With_Invalid_UserId_Should_Return_Result_Fail()
    {
        // Arrange
        var cmd = new VerifyNonceCommand()
        {
            UserId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            Nonce = "nonce",
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Command_With_Invalid_NonceID_Should_Return_Result_Fail()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var cmd = new VerifyNonceCommand()
        {
            UserId = user.Id,
            DeviceId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            Nonce = "nonce",
        };

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Command_With_Invalid_Nonce_Should_Return_Result_Fail()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        // add nonce to cache
        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        // add user to db
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var signature = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey);

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = "",
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Signature_Over_Raw_Nonce_Without_Label_Should_Return_Unauthorized()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(Convert.FromBase64String(privateKey), 0));
        var rawNonce = Convert.FromBase64String(nonce.NonceValue);
        signer.BlockUpdate(rawNonce, 0, rawNonce.Length);

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Convert.ToBase64String(signer.GenerateSignature()),
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Command_With_Valid_Nonce_Should_Return_Result_Ok()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        // add nonce to cache
        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        // add user to db
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var signature = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey);

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = signature,
            Announce = true,
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Jwt.Should().NotBeNullOrEmpty();
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        Db.OutboxMessage.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_Given_Valid_Nonce_Without_Announce_Should_Not_Publish_Login()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Jwt.Should().NotBeNullOrEmpty();
        Db.OutboxMessage.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_Given_Deleted_User_Should_Return_Unauthorized_And_Not_Announce()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);
        user.SetDelete();

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
            Announce = true,
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        Db.OutboxMessage.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_Given_Nonce_Issued_To_Another_User_Should_Return_Unauthorized()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        var nonce = Nonce.Create(Guid.NewGuid(), device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Nonce_Issued_To_Another_Device_Should_Return_Unauthorized()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        var otherDevice = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);
        user.AddDevice(otherDevice);

        var nonce = Nonce.Create(user.Id, otherDevice.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Same_Nonce_Twice_Should_Only_Succeed_Once()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        user.AddDevice(device);

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
            Announce = true,
        };

        var first = await _sut.Handle(cmd, CancellationToken.None);

        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        // Act
        var second = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
        second.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        Db.OutboxMessage.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_Given_Deleted_Device_With_Valid_Signature_Should_Return_DeviceRevoked()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";
        const string privateKey = "mR6AP1dNY1eEp7Z7bn6q0gPiOvcDl3FX4th65LY3Zwg=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        device.SetDelete();
        user.AddDevice(device);

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = Ed25519Helper.SignNonce(nonce.NonceValue, privateKey),
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.DeviceRevoked());
        Db.OutboxMessage.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_Given_Deleted_Device_With_Invalid_Signature_Should_Return_Unauthorized()
    {
        // Arrange
        const string publicKey = "VlmK9Smh3RVtT7CHaHW5rbrYAWeM9ImVdP6WhmnMqK0=";

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        var device = new Device(Guid.NewGuid(), publicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        device.SetDelete();
        user.AddDevice(device);

        var nonce = Nonce.Create(user.Id, device.Id);
        await Cache.SetAsync(nonce.Id.ToString(), nonce, CancellationToken.None, TimeSpan.FromMinutes(10));

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var cmd = new VerifyNonceCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NonceId = nonce.Id,
            Nonce = "",
        };

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }
}
