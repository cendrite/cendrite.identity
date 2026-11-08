using System.Text;
using Cendrite.Domain.Common;
using Cendrite.Message.Contracts.Messages.Profile;
using Cendrite.Message.Contracts.Responses.Profile;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.User.Commands.Signup;
using Identity.Application.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.CommandTest.SignupTest;

public class SignupCommandHandlerTest
{
    private static readonly Error SignupFailed = Errors.General.UnspecifiedError("Signup failed");
    private static readonly string AccountKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string DeviceKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly ISignupChallengeCache _fakeCache = A.Fake<ISignupChallengeCache>();
    private readonly IProofOfWorkService _fakeProofOfWork = A.Fake<IProofOfWorkService>();
    private readonly IVerifyNonceService _fakeVerifier = A.Fake<IVerifyNonceService>();
    private readonly IDeviceService _fakeDeviceService = A.Fake<IDeviceService>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly IRequestClient<CreateUserProfileMessage> _fakeProfileClient = A.Fake<IRequestClient<CreateUserProfileMessage>>();
    private readonly IChatClientService _fakeClientClient = A.Fake<IChatClientService>();
    private readonly SignupCommandHandler _sut;

    private readonly SignupChallenge _challenge = SignupChallenge.Create(20, TimeSpan.FromMinutes(5));

    public SignupCommandHandlerTest()
    {
        _sut = new SignupCommandHandler(_fakeRepo, _fakeCache, _fakeProofOfWork, _fakeVerifier, _fakeDeviceService, _fakeUnitOfWork,
            _fakeProfileClient, _fakeClientClient, A.Fake<ILogger<SignupCommandHandler>>());

        A.CallTo(() => _fakeCache.GetChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(_challenge);
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeProofOfWork.IsSolved(A<byte[]>._, A<ulong>._, A<int>._)).Returns(true);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns((User?)null);
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._))
            .ReturnsLazily((User user, string key, string signal, string version, DeviceType type, DevicePlatform platform, string? approvedBy, string? approval) =>
                new Device(Guid.NewGuid(), key, signal, version, type, platform, user.Id, approvedBy, approval));
        RespondFromProfile(isSuccess: true);
        RespondFromChatServer(isSuccess: true);
    }

    private SignupCommand Command() => new()
    {
        Username = "validUser",
        AccountPublicKey = AccountKey,
        ChallengeId = _challenge.Id,
        PowNonce = 42,
        ChallengeSignature = "challenge-signature",
        DevicePublicKey = DeviceKey,
        SignalIdentityKey = "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        DeviceApproval = "device-approval",
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Linux,
    };

    private void RespondFromProfile(bool isSuccess)
    {
        var response = A.Fake<Response<CreateUserProfileResponse>>();
        A.CallTo(() => response.Message).Returns(new CreateUserProfileResponse { CorrelationId = Guid.NewGuid(), IsSuccess = isSuccess, Error = isSuccess ? null : "Username taken" });
        A.CallTo(() => _fakeProfileClient.GetResponse<CreateUserProfileResponse>(A<CreateUserProfileMessage>._, A<CancellationToken>._, A<RequestTimeout>._))
            .Returns(response);
    }

    private void RespondFromChatServer(bool isSuccess) =>
        A.CallTo(() => _fakeClientClient.CreateClientAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Returns(isSuccess ? Result.Ok() : Result.Fail(Errors.General.UnspecifiedError("Failed to create client")));

    private void AssertNothingCreated()
    {
        A.CallTo(() => _fakeRepo.CreateAsync(A<User>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Valid_Signup_Should_Create_User_With_Account_Approved_Device()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeRepo.CreateAsync(A<User>.That.Matches(u => u.Id == result.Value!.UserId && u.AccountPublicKey == AccountKey), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, DeviceKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            DeviceConstants.ApprovedByAccount, "device-approval")).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeCache.DeleteChallengeAsync(_challenge.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Should_Verify_Both_Signatures_Over_Their_Labelled_Messages()
    {
        // Arrange
        var challengeBytes = Convert.FromBase64String(_challenge.ChallengeValue);
        var signupMessage = Convert.ToBase64String([.. Encoding.ASCII.GetBytes("cendrite-signup-v1"), .. challengeBytes]);
        var approvalMessage = Convert.ToBase64String([
            .. Encoding.ASCII.GetBytes("device-approval-v1"), .. Convert.FromBase64String(AccountKey), .. Convert.FromBase64String(DeviceKey), .. Convert.FromBase64String("BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]);

        // Act
        await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeVerifier.VerifyNonce(signupMessage, "challenge-signature", AccountKey)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeVerifier.VerifyNonce(approvalMessage, "device-approval", AccountKey)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Already_Claimed_Challenge_Should_Fail_Before_Checking_The_Work()
    {
        // Arrange
        A.CallTo(() => _fakeCache.TryClaimChallengeAsync(_challenge.Id, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(SignupFailed);
        A.CallTo(() => _fakeProofOfWork.IsSolved(A<byte[]>._, A<ulong>._, A<int>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Unsolved_Proof_Of_Work_Should_Fail_Before_Checking_Signatures()
    {
        // Arrange
        A.CallTo(() => _fakeProofOfWork.IsSolved(A<byte[]>._, A<ulong>._, A<int>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(SignupFailed);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Signature_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(SignupFailed);
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Existing_Account_Key_Should_Fail_Generically()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns(new User(Guid.NewGuid(), AccountKey));

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(SignupFailed);
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_When_Profile_Creation_Fails_Should_Return_Its_Error_And_Not_Save()
    {
        // Arrange
        RespondFromProfile(isSuccess: false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.UnspecifiedError("Username taken"));
        A.CallTo(() => _fakeClientClient.CreateClientAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_ChatServer_Fails_Should_Not_Save()
    {
        // Arrange
        RespondFromChatServer(isSuccess: false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(SignupFailed);
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
