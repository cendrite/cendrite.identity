using System.Text;
using Cendrite.Message.Contracts.Messages.Client;
using FluentAssertions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.BeginRecovery;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.User.Commands.Recover;
using Identity.Application.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.CommandTest.RecoverTest;

public class RecoverCommandHandlerTest : IntegrationTestBase
{
    private static readonly Error RecoveryFailed = Errors.General.UnspecifiedError("Recovery failed");

    private readonly BeginRecoveryCommandHandler _begin;
    private readonly RecoverCommandHandler _sut;

    public RecoverCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        var cache = sp.GetRequiredService<IRecoveryChallengeCache>();

        _begin = new BeginRecoveryCommandHandler(cache, sp.GetRequiredService<IRateLimiter>());
        _sut = new RecoverCommandHandler(
            sp.GetRequiredService<IUserRepository>(),
            cache,
            sp.GetRequiredService<IVerifyNonceService>(),
            sp.GetRequiredService<IDeviceService>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IChatClientService>(),
            sp.GetRequiredService<ILogger<RecoverCommandHandler>>());
    }

    [Fact]
    public async Task Recover_Should_Add_An_Account_Approved_Device_To_The_Existing_Account()
    {
        // Arrange
        var account = new KeyPair();
        var user = await SaveUser(account, withExistingDevice: true);
        var device = new KeyPair();
        var cmd = await BuildCommand(account, device);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.UserId.Should().Be(user.Id);

        Db.ChangeTracker.Clear();
        var devices = Db.Device.Where(d => d.UserId == user.Id).ToList();
        devices.Should().HaveCount(2);
        var recovered = devices.Single(d => d.Id == result.Value.DeviceId);
        recovered.PublicKey.Should().Be(device.PublicKey);
        recovered.ConnectionId.Should().Be(result.Value.DeviceConnectionId);
        recovered.ApprovedBy.Should().Be(DeviceConstants.ApprovedByAccount);
        recovered.ApprovalSignature.Should().Be(cmd.DeviceApproval);
        Harness.Published.Select<CreateClientMessage>().Should().Contain(m => m.Context.Message.DeviceId == recovered.Id);
        Db.OutboxMessage.Should().ContainSingle(m => m.Type.Contains(nameof(DeviceAddedEvent)));
    }

    [Fact]
    public async Task Recover_Given_Key_Without_Account_Should_Fail_Generically()
    {
        // Act
        var result = await _sut.Handle(await BuildCommand(new KeyPair(), new KeyPair()), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_Given_Deleted_Account_Should_Fail_Generically()
    {
        // Arrange
        var account = new KeyPair();
        var user = await SaveUser(account);
        user.SetDelete();
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(await BuildCommand(account, new KeyPair()), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_Given_Challenge_Issued_For_Another_Key_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        await SaveUser(account);
        var cmd = await BuildCommand(account, new KeyPair());
        var otherChallenge = await _begin.Handle(new BeginRecoveryCommand { AccountPublicKey = new KeyPair().PublicKey }, CancellationToken.None);

        // Act
        var result = await _sut.Handle(cmd with { ChallengeId = otherChallenge.Value!.ChallengeId }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
    }

    [Fact]
    public async Task Recover_Given_Reused_Challenge_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        await SaveUser(account);
        var cmd = await BuildCommand(account, new KeyPair());
        await _sut.Handle(cmd, CancellationToken.None);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        Db.Device.Should().HaveCount(1);
    }

    [Fact]
    public async Task Recover_Given_Challenge_Signed_By_Another_Key_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        await SaveUser(account);
        var cmd = await BuildCommand(account, new KeyPair());
        var forged = await BuildCommand(new KeyPair(), new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with { ChallengeSignature = forged.ChallengeSignature }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_Given_Approval_For_Another_Device_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        await SaveUser(account);
        var cmd = await BuildCommand(account, new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with { DevicePublicKey = new KeyPair().PublicKey }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Recover_Given_Signup_Label_Instead_Of_Recovery_Label_Should_Fail()
    {
        // Arrange
        var account = new KeyPair();
        await SaveUser(account);
        var challenge = await _begin.Handle(new BeginRecoveryCommand { AccountPublicKey = account.PublicKey }, CancellationToken.None);
        var challengeBytes = Convert.FromBase64String(challenge.Value!.Challenge);
        var cmd = await BuildCommand(account, new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with
        {
            ChallengeId = challenge.Value.ChallengeId,
            ChallengeSignature = account.Sign([.. Encoding.ASCII.GetBytes("cendrite-signup-v1"), .. challengeBytes]),
        }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().BeEmpty();
    }

    private async Task<User> SaveUser(KeyPair account, bool withExistingDevice = false)
    {
        var user = new User(Guid.NewGuid(), account.PublicKey);
        if (withExistingDevice)
            user.AddDevice(new Device(Guid.NewGuid(), new KeyPair().PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id));
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    private async Task<RecoverCommand> BuildCommand(KeyPair account, KeyPair device)
    {
        var challenge = await _begin.Handle(new BeginRecoveryCommand { AccountPublicKey = account.PublicKey }, CancellationToken.None);
        var challengeBytes = Convert.FromBase64String(challenge.Value!.Challenge);

        return new RecoverCommand
        {
            AccountPublicKey = account.PublicKey,
            ChallengeId = challenge.Value.ChallengeId,
            ChallengeSignature = account.Sign([.. Encoding.ASCII.GetBytes("cendrite-recovery-v1"), .. challengeBytes]),
            DevicePublicKey = device.PublicKey,
            DeviceApproval = account.SignDeviceApproval(account.PublicKey, device.PublicKey),
            DeviceAppVersion = "1.0",
            DeviceType = DeviceType.Desktop,
            DevicePlatform = DevicePlatform.Linux,
        };
    }
}
