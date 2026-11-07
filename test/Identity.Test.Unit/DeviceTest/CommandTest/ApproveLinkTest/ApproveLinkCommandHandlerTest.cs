using System.Text;
using Cendrite.Domain.Common;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Features.Device.Commands.ApproveLink;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.CommandTest.ApproveLinkTest;

public class ApproveLinkCommandHandlerTest
{
    private static readonly string AccountKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly string NewDeviceKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray());

    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly IVerifyNonceService _fakeVerifier = A.Fake<IVerifyNonceService>();
    private readonly IDeviceService _fakeDeviceService = A.Fake<IDeviceService>();
    private readonly IUnitOfWork _fakeUnitOfWork = A.Fake<IUnitOfWork>();
    private readonly IChatClientService _fakeClient = A.Fake<IChatClientService>();
    private readonly ApproveLinkCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), AccountKey);
    private readonly Device _approver;
    private readonly PendingLink _pending;

    public ApproveLinkCommandHandlerTest()
    {
        _sut = new ApproveLinkCommandHandler(_fakeRepo, _fakeCache, _fakeVerifier, _fakeDeviceService, _fakeUnitOfWork, _fakeClient,
            A.Fake<ILogger<ApproveLinkCommandHandler>>());

        _approver = new Device(Guid.NewGuid(), "approver-key", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_approver);
        _pending = new PendingLink(Guid.NewGuid(), _user.Id, _approver.Id, NewDeviceKey, "1.0", DeviceType.Desktop, DevicePlatform.Windows,
            "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));

        A.CallTo(() => _fakeCache.GetPendingAsync(_pending.Id, A<CancellationToken>._)).Returns(_pending);
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(_pending.Id, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(true);
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._))
            .ReturnsLazily((User user, string key, string version, DeviceType type, DevicePlatform platform, string? approvedBy, string? approval) =>
                new Device(Guid.NewGuid(), key, version, type, platform, user.Id, approvedBy, approval));
        RespondFromChatServer(isSuccess: true);
    }

    private ApproveLinkCommand Command(Guid? deviceId = null) => new()
    {
        UserId = _user.Id,
        DeviceId = deviceId ?? _approver.Id,
        PendingLinkId = _pending.Id,
        Approval = "approval",
    };

    private void RespondFromChatServer(bool isSuccess) =>
        A.CallTo(() => _fakeClient.CreateClientAsync(A<Guid>._, A<Guid>._, A<Guid>._, A<CancellationToken>._))
            .Returns(isSuccess ? Result.Ok() : Result.Fail(Errors.General.UnspecifiedError("Failed to create client")));

    private void AssertNothingCreated()
    {
        A.CallTo(() => _fakeDeviceService.RegisterDevice(A<User>._, A<string>._, A<string>._, A<DeviceType>._, A<DevicePlatform>._, A<string?>._, A<string?>._))
            .MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Valid_Approval_Should_Add_Device_Approved_By_The_Issuing_Device()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeDeviceService.RegisterDevice(_user, NewDeviceKey, "1.0", DeviceType.Desktop, DevicePlatform.Windows,
            _approver.Id.ToString(), "approval")).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeCache.SetPendingAsync(A<PendingLink>.That.Matches(p => p.Status == PendingLinkStatus.Approved && p.DeviceId != null),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        _user.DomainEvents.Should().ContainSingle(e => e is DeviceAddedEvent);
    }

    [Fact]
    public async Task Handle_Should_Verify_The_Labelled_Approval_Against_The_Approving_Device_Key()
    {
        // Arrange
        var message = Convert.ToBase64String([
            .. Encoding.ASCII.GetBytes("device-approval-v1"), .. Convert.FromBase64String(AccountKey), .. Convert.FromBase64String(NewDeviceKey)]);

        // Act
        await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        A.CallTo(() => _fakeVerifier.VerifyNonce(message, "approval", "approver-key")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_From_Another_Device_Should_Fail_Without_Verifying()
    {
        // Act
        var result = await _sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_pending.Id));
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Invalid_Signature_Should_Fail_Before_Claiming()
    {
        // Arrange
        A.CallTo(() => _fakeVerifier.VerifyNonce(A<string>._, A<string>._, A<string>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_Given_Already_Decided_Link_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(_pending.Id, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_pending.Id));
        AssertNothingCreated();
    }

    [Fact]
    public async Task Handle_When_ChatServer_Fails_Should_Release_The_Decision_And_Not_Save()
    {
        // Arrange
        RespondFromChatServer(isSuccess: false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        A.CallTo(() => _fakeCache.ReleaseDecisionAsync(_pending.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }
}
