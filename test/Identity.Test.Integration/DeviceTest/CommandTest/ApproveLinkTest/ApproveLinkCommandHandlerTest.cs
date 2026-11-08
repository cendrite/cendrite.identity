using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.Device.Commands.ApproveLink;
using Identity.Application.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.CommandTest.ApproveLinkTest;

public class ApproveLinkCommandHandlerTest : IntegrationTestBase
{
    private readonly IDeviceLinkCache _cache;
    private readonly ApproveLinkCommandHandler _sut;

    public ApproveLinkCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IDeviceLinkCache>();
        _sut = new ApproveLinkCommandHandler(
            sp.GetRequiredService<IUserRepository>(),
            _cache,
            sp.GetRequiredService<IVerifyNonceService>(),
            sp.GetRequiredService<IDeviceService>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<IChatClientService>(),
            sp.GetRequiredService<ILogger<ApproveLinkCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Valid_Approval_Should_Create_Device_Approved_By_The_Issuing_Device()
    {
        // Arrange
        var (user, issuer, issuerKey) = await SaveUserWithDevice();
        var newDevice = new KeyPair();
        var pending = await AddPending(user, issuer, newDevice);
        var approval = issuerKey.SignDeviceApproval(user.AccountPublicKey, newDevice.PublicKey, pending.SignalIdentityKey);

        // Act
        var result = await _sut.Handle(Command(user, issuer, pending, approval), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var approved = await _cache.GetPendingAsync(pending.Id, CancellationToken.None);
        approved!.Status.Should().Be(PendingLinkStatus.Approved);

        Db.ChangeTracker.Clear();
        var created = Db.Device.Single(d => d.Id == approved.DeviceId);
        created.PublicKey.Should().Be(newDevice.PublicKey);
        created.SignalIdentityKey.Should().Be(pending.SignalIdentityKey);
        created.ApprovedBy.Should().Be(issuer.Id.ToString());
        created.ApprovalSignature.Should().Be(approval);
        Db.OutboxMessage.Should().Contain(m => m.Type.Contains(nameof(DeviceAddedEvent)));
    }

    [Fact]
    public async Task Handle_Given_Approval_Signed_By_Another_Key_Should_Fail()
    {
        // Arrange
        var (user, issuer, _) = await SaveUserWithDevice();
        var newDevice = new KeyPair();
        var pending = await AddPending(user, issuer, newDevice);
        var forged = new KeyPair().SignDeviceApproval(user.AccountPublicKey, newDevice.PublicKey, pending.SignalIdentityKey);

        // Act
        var result = await _sut.Handle(Command(user, issuer, pending, forged), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_Given_Rejected_Link_Should_Fail()
    {
        // Arrange
        var (user, issuer, issuerKey) = await SaveUserWithDevice();
        var newDevice = new KeyPair();
        var pending = await AddPending(user, issuer, newDevice, PendingLinkStatus.Rejected);
        var approval = issuerKey.SignDeviceApproval(user.AccountPublicKey, newDevice.PublicKey, pending.SignalIdentityKey);

        // Act
        var result = await _sut.Handle(Command(user, issuer, pending, approval), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().HaveCount(1);
    }

    private static ApproveLinkCommand Command(User user, Device issuer, PendingLink pending, string approval) => new()
    {
        UserId = user.Id,
        DeviceId = issuer.Id,
        PendingLinkId = pending.Id,
        Approval = approval,
    };

    private async Task<PendingLink> AddPending(User user, Device issuer, KeyPair device, PendingLinkStatus status = PendingLinkStatus.Pending)
    {
        var pending = new PendingLink(Guid.NewGuid(), user.Id, issuer.Id, device.PublicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            "poll-secret-hash", DateTime.UtcNow.AddMinutes(5), status);
        await _cache.SetPendingAsync(pending, CancellationToken.None);
        return pending;
    }

    private async Task<(User, Device, KeyPair)> SaveUserWithDevice()
    {
        var user = new User(Guid.NewGuid(), new KeyPair().PublicKey, UserTags.New());
        var key = new KeyPair();
        var device = new Device(Guid.NewGuid(), key.PublicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return (user, device, key);
    }
}
