using System.Security.Cryptography;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.RedeemLinkCode;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.CommandTest.RedeemLinkCodeTest;

public class RedeemLinkCodeCommandHandlerTest
{
    private static readonly Error LinkFailed = Errors.General.UnspecifiedError("Linking failed");
    private const string Code = "ABCDE10110";

    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly RedeemLinkCodeCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), "account-key");
    private readonly Device _issuer;
    private readonly LinkCode _code;

    public RedeemLinkCodeCommandHandlerTest()
    {
        _sut = new RedeemLinkCodeCommandHandler(_fakeRepo, _fakeCache, _fakeUnitOfWork, A.Fake<ILogger<RedeemLinkCodeCommandHandler>>());

        _issuer = new Device(Guid.NewGuid(), "issuer-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_issuer);
        _code = new LinkCode(Code, _user.Id, _issuer.Id, DateTime.UtcNow.AddMinutes(5));

        A.CallTo(() => _fakeCache.GetCodeAsync(Code, A<CancellationToken>._)).Returns(_code);
        A.CallTo(() => _fakeCache.TryClaimCodeAsync(Code, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
    }

    private static RedeemLinkCodeCommand Command(string code = Code) => new()
    {
        Code = code,
        DevicePublicKey = "new-device-key",
        SignalIdentityKey = "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Windows,
    };

    private void AssertNoPendingLink()
    {
        A.CallTo(() => _fakeCache.SetPendingAsync(A<PendingLink>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Valid_Code_Should_Store_Pending_Link_And_Raise_Event()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var secretHash = Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(result.Value!.PollSecret)));
        A.CallTo(() => _fakeCache.SetPendingAsync(A<PendingLink>.That.Matches(p =>
                p.Id == result.Value.PendingLinkId
                && p.UserId == _user.Id
                && p.IssuingDeviceId == _issuer.Id
                && p.DevicePublicKey == "new-device-key"
                && p.DevicePlatform == DevicePlatform.Windows
                && p.PollSecretHash == secretHash
                && p.ExpiresAt == _code.ExpiresAt), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeCache.DeleteCodeAsync(Code, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _user.DomainEvents.Should().ContainSingle(e => e is DeviceLinkRequestedEvent
            && ((DeviceLinkRequestedEvent)e).IssuingConnectionId == _issuer.ConnectionId);
    }

    [Fact]
    public async Task Handle_Should_Normalize_The_Code_Before_Looking_It_Up()
    {
        // Act
        var result = await _sut.Handle(Command("abcde-1oil0"), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeCache.GetCodeAsync(Code, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_Given_Expired_Code_Should_Fail_Before_Claiming()
    {
        // Arrange
        A.CallTo(() => _fakeCache.GetCodeAsync(Code, A<CancellationToken>._)).Returns(_code with { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) });

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(LinkFailed);
        A.CallTo(() => _fakeCache.TryClaimCodeAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNoPendingLink();
    }

    [Fact]
    public async Task Handle_Given_Already_Claimed_Code_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeCache.TryClaimCodeAsync(Code, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(LinkFailed);
        AssertNoPendingLink();
    }

    [Fact]
    public async Task Handle_When_Issuing_Device_Is_Gone_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeCache.GetCodeAsync(Code, A<CancellationToken>._)).Returns(_code with { IssuingDeviceId = Guid.NewGuid() });

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(LinkFailed);
        AssertNoPendingLink();
    }
}
