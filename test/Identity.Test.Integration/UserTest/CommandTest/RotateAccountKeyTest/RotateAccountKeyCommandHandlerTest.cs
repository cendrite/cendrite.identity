using System.Text;
using FluentAssertions;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.User.Commands.RotateAccountKey;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.CommandTest.RotateAccountKeyTest;

public class RotateAccountKeyCommandHandlerTest : IntegrationTestBase
{
    private readonly RotateAccountKeyCommandHandler _sut;

    public RotateAccountKeyCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _sut = new RotateAccountKeyCommandHandler(
            sp.GetRequiredService<IUserRepository>(),
            sp.GetRequiredService<IVerifyNonceService>(),
            sp.GetRequiredService<IRateLimiter>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ILogger<RotateAccountKeyCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Valid_Rotation_Should_Store_The_New_Key_And_The_Signed_Record()
    {
        // Arrange
        var (user, device, deviceKey) = await SaveUserWithDevice(new KeyPair());
        var newAccount = new KeyPair();
        var cmd = Command(user, device, deviceKey, newAccount);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        Db.ChangeTracker.Clear();
        Db.User.Single(u => u.Id == user.Id).AccountPublicKey.Should().Be(newAccount.PublicKey);
        var rotation = Db.AccountKeyRotation.Single(r => r.UserId == user.Id);
        rotation.DeviceId.Should().Be(device.Id);
        rotation.OldAccountPublicKey.Should().Be(user.AccountPublicKey);
        rotation.NewAccountPublicKey.Should().Be(newAccount.PublicKey);
        rotation.Proof.Should().Be(cmd.Proof);
        rotation.Approval.Should().Be(cmd.Approval);
        Db.OutboxMessage.Should().Contain(m => m.Type.Contains(nameof(AccountKeyRotatedEvent)));
    }

    [Fact]
    public async Task Handle_Given_Approval_From_Another_Device_Key_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        var (user, device, _) = await SaveUserWithDevice(account);
        var cmd = Command(user, device, new KeyPair(), new KeyPair());

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.ChangeTracker.Clear();
        Db.User.Single(u => u.Id == user.Id).AccountPublicKey.Should().Be(account.PublicKey);
        Db.AccountKeyRotation.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Key_Of_Another_Account_Should_Fail()
    {
        // Arrange
        var (user, device, deviceKey) = await SaveUserWithDevice(new KeyPair());
        var otherAccount = new KeyPair();
        await SaveUserWithDevice(otherAccount);

        // Act
        var result = await _sut.Handle(Command(user, device, deviceKey, otherAccount), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.AccountKeyRotation.Should().BeEmpty();
    }

    private static RotateAccountKeyCommand Command(User user, Device device, KeyPair deviceKey, KeyPair newAccount)
    {
        byte[] message = [
            .. Encoding.ASCII.GetBytes("account-rotate-v1"),
            .. Convert.FromBase64String(user.AccountPublicKey),
            .. Convert.FromBase64String(newAccount.PublicKey)];

        return new RotateAccountKeyCommand
        {
            UserId = user.Id,
            DeviceId = device.Id,
            NewAccountPublicKey = newAccount.PublicKey,
            Proof = newAccount.Sign(message),
            Approval = deviceKey.Sign(message),
        };
    }

    private async Task<(User, Device, KeyPair)> SaveUserWithDevice(KeyPair account)
    {
        var user = new User(Guid.NewGuid(), account.PublicKey);
        var key = new KeyPair();
        var device = new Device(Guid.NewGuid(), key.PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return (user, device, key);
    }
}
