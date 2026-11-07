using System.Text;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.User.Commands.RotateAccountKey;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.CommandTest.RotateAccountKeyTest;

public class RotateAccountKeyCommandHandlerTest
{
    private static readonly Error RotationFailed = Errors.General.UnspecifiedError("Rotation failed");
    private static readonly string OldKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string NewKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IVerifyNonceService _fakeVerifier = A.Fake<IVerifyNonceService>();
    private readonly IRateLimiter _fakeRateLimiter = A.Fake<IRateLimiter>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly RotateAccountKeyCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), OldKey);
    private readonly Device _device;

    public RotateAccountKeyCommandHandlerTest()
    {
        _sut = new RotateAccountKeyCommandHandler(_fakeRepo, _fakeVerifier, _fakeRateLimiter, _fakeUnitOfWork,
            A.Fake<ILogger<RotateAccountKeyCommandHandler>>());

        _device = new Device(Guid.NewGuid(), "device-key", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_device);

        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns((User?)null);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(true);
    }

    private RotateAccountKeyCommand Command(Guid? deviceId = null) => new()
    {
        UserId = _user.Id,
        DeviceId = deviceId ?? _device.Id,
        NewAccountPublicKey = NewKey,
        Proof = "proof",
        Approval = "approval",
    };

    private void AssertNothingChanged()
    {
        _user.AccountPublicKey.Should().Be(OldKey);
        _user.AccountKeyRotations.Should().BeEmpty();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Valid_Rotation_Should_Swap_The_Key_Record_It_And_Raise_Event()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _user.AccountPublicKey.Should().Be(NewKey);
        _user.AccountKeyRotations.Should().ContainSingle(r => r.DeviceId == _device.Id && r.OldAccountPublicKey == OldKey && r.Proof == "proof" && r.Approval == "approval");
        _user.DomainEvents.Should().ContainSingle(e => e is AccountKeyRotatedEvent && ((AccountKeyRotatedEvent)e).ConnectionId == _device.ConnectionId);
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Should_Verify_Both_Signatures_Over_The_Labelled_Old_To_New_Message()
    {
        // Arrange
        var message = Convert.ToBase64String([
            .. Encoding.ASCII.GetBytes("account-rotate-v1"), .. Convert.FromBase64String(OldKey), .. Convert.FromBase64String(NewKey)]);

        // Act
        await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeVerifier.VerifyNonce(message, "proof", NewKey)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeVerifier.VerifyNonce(message, "approval", "device-key")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Rate_Limited_Should_Fail_Without_Looking_Up_The_User()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNothingChanged();
    }

    [Fact]
    public async Task Handle_Should_Rate_Limit_Per_Device_So_One_Device_Cannot_Use_Up_The_Others()
    {
        // Act
        await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync($"account-key:rotate:{_device.Id}", 3, TimeSpan.FromDays(1), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Device_Not_On_The_Account_Should_Fail_Without_Verifying()
    {
        // Act
        var result = await _sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        AssertNothingChanged();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Signature_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, "approval", A<string>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RotationFailed);
        AssertNothingChanged();
    }

    [Fact]
    public async Task Handle_Given_Key_Already_In_Use_Should_Fail_Generically()
    {
        // Arrange
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByAccountKeySpec>._, A<CancellationToken>._)).Returns(new User(Guid.NewGuid(), NewKey));

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(RotationFailed);
        AssertNothingChanged();
    }
}
