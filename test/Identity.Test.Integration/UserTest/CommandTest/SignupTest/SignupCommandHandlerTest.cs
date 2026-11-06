using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Messages.Profile;
using FluentAssertions;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.User.Commands.Signup;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.CommandTest.SignupTest;

public class SignupCommandHandlerTest : IntegrationTestBase
{
    private const int Difficulty = 8;

    private readonly SignupCommandHandler _sut;
    private readonly ISignupChallengeCache _challengeCache;

    public SignupCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        _challengeCache = sp.GetRequiredService<ISignupChallengeCache>();

        _sut = new SignupCommandHandler(
            sp.GetRequiredService<IUserRepository>(),
            _challengeCache,
            sp.GetRequiredService<IProofOfWorkService>(),
            sp.GetRequiredService<IVerifyNonceService>(),
            sp.GetRequiredService<IDeviceService>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IRequestClient<CreateUserProfileMessage>>(),
            sp.GetRequiredService<IRequestClient<CreateClientMessage>>(),
            sp.GetRequiredService<ILogger<SignupCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Valid_Signup_Should_Create_User_And_Approved_Device()
    {
        // Arrange
        var account = new KeyPair();
        var device = new KeyPair();
        var cmd = await BuildCommand(account, device);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var user = Db.User.Single();
        user.Id.Should().Be(result.Value!.UserId);
        user.AccountPublicKey.Should().Be(account.PublicKey);

        var stored = Db.Device.Single();
        stored.Id.Should().Be(result.Value.DeviceId);
        stored.ConnectionId.Should().Be(result.Value.DeviceConnectionId);
        stored.UserId.Should().Be(user.Id);
        stored.PublicKey.Should().Be(device.PublicKey);
        stored.Platform.Should().Be(DevicePlatform.Linux);
        stored.ApprovedBy.Should().Be(DeviceConstants.ApprovedByAccount);
        stored.ApprovalSignature.Should().Be(cmd.DeviceApproval);

        Harness.Published.Select<CreateUserProfileMessage>().Should().ContainSingle(m => m.Context.Message.UserId == user.Id);
        Harness.Published.Select<CreateClientMessage>().Should().ContainSingle(m => m.Context.Message.DeviceId == stored.Id && m.Context.Message.ConnectionId == stored.ConnectionId);
        (await _challengeCache.GetChallengeAsync(cmd.ChallengeId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Handle_Given_Reused_Challenge_Should_Return_Generic_Failure()
    {
        // Arrange
        var cmd = await BuildCommand(new KeyPair(), new KeyPair());
        await _sut.Handle(cmd, CancellationToken.None);

        // Act
        var result = await _sut.Handle(cmd with { Username = "otherUser" }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        Db.User.Should().HaveCount(1);
        Db.Device.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_Given_Unknown_Challenge_Should_Return_Generic_Failure()
    {
        // Arrange
        var cmd = await BuildCommand(new KeyPair(), new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with { ChallengeId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        Db.User.Should().BeEmpty();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Wrong_Proof_Of_Work_Should_Fail_And_Burn_Challenge()
    {
        // Arrange
        var cmd = await BuildCommand(new KeyPair(), new KeyPair());
        var challenge = (await _challengeCache.GetChallengeAsync(cmd.ChallengeId, CancellationToken.None))!;
        var wrongNonce = FindFailingNonce(Convert.FromBase64String(challenge.ChallengeValue));

        // Act
        var result = await _sut.Handle(cmd with { PowNonce = wrongNonce }, CancellationToken.None);
        var retry = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        retry.Success.Should().BeFalse();
        Db.User.Should().BeEmpty();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Challenge_Signed_By_Another_Key_Should_Return_Generic_Failure()
    {
        // Arrange
        var account = new KeyPair();
        var cmd = await BuildCommand(account, new KeyPair());
        var forged = await BuildCommand(new KeyPair(), new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with { ChallengeSignature = forged.ChallengeSignature }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        Db.User.Should().BeEmpty();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Approval_For_Another_Device_Should_Return_Generic_Failure()
    {
        // Arrange
        var account = new KeyPair();
        var cmd = await BuildCommand(account, new KeyPair());

        // Act
        var result = await _sut.Handle(cmd with { DevicePublicKey = new KeyPair().PublicKey }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        Db.User.Should().BeEmpty();
        Db.Device.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Existing_Account_Key_Should_Return_Generic_Failure()
    {
        // Arrange
        var account = new KeyPair();
        await _sut.Handle(await BuildCommand(account, new KeyPair()), CancellationToken.None);

        // Act
        var result = await _sut.Handle(await BuildCommand(account, new KeyPair()), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Signup failed"));
        Db.User.Should().HaveCount(1);
    }

    private async Task<SignupCommand> BuildCommand(KeyPair account, KeyPair device)
    {
        var challenge = SignupChallenge.Create(Difficulty, TimeSpan.FromMinutes(5));
        await _challengeCache.AddChallengeAsync(challenge, CancellationToken.None);
        var challengeBytes = Convert.FromBase64String(challenge.ChallengeValue);

        return new SignupCommand
        {
            Username = "validUser",
            AccountPublicKey = account.PublicKey,
            ChallengeId = challenge.Id,
            PowNonce = Solve(challengeBytes),
            ChallengeSignature = account.Sign(Concat(Encoding.ASCII.GetBytes("cendrite-signup-v1"), challengeBytes)),
            DevicePublicKey = device.PublicKey,
            DeviceApproval = account.SignDeviceApproval(account.PublicKey, device.PublicKey),
            DeviceAppVersion = "1.0",
            DeviceType = DeviceType.Desktop,
            DevicePlatform = DevicePlatform.Linux,
        };
    }

    private static ulong Solve(byte[] challenge)
    {
        for (ulong n = 0; ; n++)
            if (LeadingZeroBits(Hash(challenge, n)) >= Difficulty) return n;
    }

    private static ulong FindFailingNonce(byte[] challenge)
    {
        for (ulong n = 0; ; n++)
            if (LeadingZeroBits(Hash(challenge, n)) < Difficulty) return n;
    }

    private static byte[] Hash(byte[] challenge, ulong n)
    {
        var input = new byte[challenge.Length + 8];
        challenge.CopyTo(input, 0);
        BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(challenge.Length), n);
        return SHA256.HashData(input);
    }

    private static int LeadingZeroBits(byte[] hash)
    {
        var count = 0;
        foreach (var b in hash)
        {
            if (b == 0) { count += 8; continue; }
            return count + System.Numerics.BitOperations.LeadingZeroCount((uint)b) - 24;
        }
        return count;
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
