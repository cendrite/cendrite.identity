using System.Text;
using Cendrite.Domain.Common;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Features.User.Commands.Recover;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.UserTest.CommandTest.RecoverTest;

public class RecoverCommandHandlerTest
{
    private static readonly Error RecoveryFailed = Errors.General.UnspecifiedError("Recovery failed");
    private static readonly string AccountKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string DeviceKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IRecoveryChallengeCache _fakeCache = A.Fake<IRecoveryChallengeCache>();
    private readonly IVerifyNonceService _fakeVerifier = A.Fake<IVerifyNonceService>();
    private readonly IDeviceService _fakeDeviceService = A.Fake<IDeviceService>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly IChatClientService _fakeClient = A.Fake<IChatClientService>();
    private readonly RecoverCommandHandler _sut;

    private readonly RecoveryChallenge _challenge = RecoveryChallenge.Create(AccountKey);
    private readonly User _user = new(Guid.NewGuid(), AccountKey, UserTags.New());

    public RecoverCommandHandlerTest()
    {
        _sut = new RecoverCommandHandler(_fakeRepo, _fakeCache, _fakeVerifier, _fakeDeviceService, _fakeUnitOfWork, _fakeClient,
            A.Fake<ILogger<RecoverCommandHandler>>());

        A.CallTo(() => _fakeCache.GetChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(_challenge);
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns(_user);
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._))
            .ReturnsLazily((User user, string key, string signal, string version, DeviceType type, DevicePlatform platform, string? approvedBy, string? approval) =>
                new Device(Guid.NewGuid(), key, signal, version, type, platform, user.Id, approvedBy, approval));
        RespondFromChatServer(isSuccess: true);
    }

    private RecoverCommand Command() => new()
    {
        AccountPublicKey = AccountKey,
        ChallengeId = _challenge.Id,
        ChallengeSignature = "challenge-signature",
        DevicePublicKey = DeviceKey,
        SignalIdentityKey = "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        DeviceApproval = "device-approval",
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Linux,
    };

    private void RespondFromChatServer(bool isSuccess) =>
        A.CallTo(() => _fakeClient.CreateClientAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Returns(isSuccess ? Result.Ok() : Result.Fail(Errors.General.UnspecifiedError("Failed to create client")));

    private void AssertNothingCreated()
    {
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._))
            .MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Valid_Recovery_Should_Add_An_Account_Approved_Device()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.UserId.Should().Be(_user.Id);
        A.CallTo(() => _fakeDeviceService.RegisterDevice(_user, DeviceKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            DeviceConstants.ApprovedByAccount, "device-approval")).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeCache.DeleteChallengeAsync(_challenge.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _user.DomainEvents.Should().ContainSingle(e => e is DeviceAddedEvent && ((DeviceAddedEvent)e).DeviceId == result.Value.DeviceId);
    }

    [Fact]
    public async Task Handle_Should_Verify_Both_Signatures_Over_Their_Labelled_Messages()
    {
        // Arrange
        var challengeBytes = Convert.FromBase64String(_challenge.ChallengeValue);
        var recoveryMessage = Convert.ToBase64String([.. Encoding.ASCII.GetBytes("cendrite-recovery-v1"), .. challengeBytes]);
        var approvalMessage = Convert.ToBase64String([
            .. Encoding.ASCII.GetBytes("device-approval-v1"), .. Convert.FromBase64String(AccountKey), .. Convert.FromBase64String(DeviceKey), .. Convert.FromBase64String("BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]);

        // Act
        await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeVerifier.VerifyNonce(recoveryMessage, "challenge-signature", AccountKey)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeVerifier.VerifyNonce(approvalMessage, "device-approval", AccountKey)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Unknown_Challenge_Should_Fail_Before_Claiming()
    {
        // Act
        var result = await _sut.Handle(Command() with { ChallengeId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Challenge_For_Another_Key_Should_Fail_Before_Claiming()
    {
        // Arrange
        var otherKey = Convert.ToBase64String(Enumerable.Repeat((byte)9, 32).ToArray());

        // Act
        var result = await _sut.Handle(Command() with { AccountPublicKey = otherKey }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Expired_Challenge_Should_Fail()
    {
        // Arrange
        var expired = RecoveryChallenge.FromCache(_challenge.Id, _challenge.ChallengeValue, AccountKey, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5));
        A.CallTo(() => _fakeCache.GetChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(expired);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Already_Claimed_Challenge_Should_Fail_Before_Checking_Signatures()
    {
        // Arrange
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Signature_Should_Fail_Without_Looking_Up_The_Account()
    {
        // Arrange
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_No_Account_For_The_Key_Should_Fail_Generically()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_When_ChatServer_Fails_Should_Not_Save()
    {
        // Arrange
        RespondFromChatServer(isSuccess: false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RecoveryFailed);
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
