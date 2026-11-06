using System.Text;
using Cendrite.Message.Contracts.Messages.Client;
using FluentAssertions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.Device.Commands.Link;
using Identity.Application.Features.Device.Queries.Link;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Identity.Test.Integration.DeviceTest.LinkTest;

public class DeviceLinkTest : IntegrationTestBase
{
    private readonly CreateLinkCodeCommandHandler _createCode;
    private readonly RedeemLinkCodeCommandHandler _redeem;
    private readonly GetLinkStatusQueryHandler _status;
    private readonly GetPendingLinkQueryHandler _details;
    private readonly ApproveLinkCommandHandler _approve;
    private readonly RejectLinkCommandHandler _reject;

    public DeviceLinkTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        var users = sp.GetRequiredService<IUserRepository>();
        var cache = sp.GetRequiredService<IDeviceLinkCache>();
        var rateLimiter = sp.GetRequiredService<IRateLimiter>();

        _createCode = new CreateLinkCodeCommandHandler(users, cache, rateLimiter);
        _redeem = new RedeemLinkCodeCommandHandler(users, cache, sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<ILogger<RedeemLinkCodeCommandHandler>>());
        _status = new GetLinkStatusQueryHandler(cache, rateLimiter);
        _details = new GetPendingLinkQueryHandler(users, cache);
        _approve = new ApproveLinkCommandHandler(users, cache, sp.GetRequiredService<IVerifyNonceService>(), sp.GetRequiredService<IDeviceService>(),
            sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<IRequestClient<CreateClientMessage>>(), sp.GetRequiredService<ILogger<ApproveLinkCommandHandler>>());
        _reject = new RejectLinkCommandHandler(cache);
    }

    [Fact]
    public async Task Link_Approved_By_The_Issuing_Device_Creates_A_Device_Signed_By_It()
    {
        // Arrange
        var (user, approver, approverKey) = await SaveUserWithDevice();
        var newDevice = new KeyPair();
        var code = await _createCode.Handle(new CreateLinkCodeCommand { UserId = user.Id, DeviceId = approver.Id }, CancellationToken.None);
        var redeemed = await _redeem.Handle(Redeem(code.Value!.Code, newDevice), CancellationToken.None);

        var details = await _details.Handle(new GetPendingLinkQuery { UserId = user.Id, DeviceId = approver.Id, PendingLinkId = redeemed.Value!.PendingLinkId }, CancellationToken.None);
        var approval = approverKey.Sign([
            .. Encoding.ASCII.GetBytes("device-approval-v1"),
            .. Convert.FromBase64String(details.Value!.AccountPublicKey),
            .. Convert.FromBase64String(details.Value.DevicePublicKey)]);

        // Act
        var approved = await _approve.Handle(new ApproveLinkCommand { UserId = user.Id, DeviceId = approver.Id, PendingLinkId = redeemed.Value.PendingLinkId, Approval = approval }, CancellationToken.None);
        var status = await _status.Handle(new GetLinkStatusQuery { PendingLinkId = redeemed.Value.PendingLinkId, PollSecret = redeemed.Value.PollSecret }, CancellationToken.None);

        // Assert
        approved.Success.Should().BeTrue();
        status.Value!.Status.Should().Be("Approved");
        details.Value.DevicePublicKey.Should().Be(newDevice.PublicKey);
        details.Value.AccountPublicKey.Should().Be(user.AccountPublicKey);

        Db.ChangeTracker.Clear();
        var created = Db.Device.Single(d => d.Id == status.Value.DeviceId);
        created.PublicKey.Should().Be(newDevice.PublicKey);
        created.ConnectionId.Should().Be(status.Value.DeviceConnectionId!.Value);
        created.ApprovedBy.Should().Be(approver.Id.ToString());
        created.ApprovalSignature.Should().Be(approval);
        Db.OutboxMessage.Should().Contain(m => m.Type.Contains(nameof(DeviceLinkRequestedEvent)));
        Db.OutboxMessage.Should().Contain(m => m.Type.Contains(nameof(DeviceAddedEvent)));
    }

    [Fact]
    public async Task A_Code_Can_Only_Be_Redeemed_Once()
    {
        // Arrange
        var (user, approver, _) = await SaveUserWithDevice();
        var code = await _createCode.Handle(new CreateLinkCodeCommand { UserId = user.Id, DeviceId = approver.Id }, CancellationToken.None);
        var formatted = code.Value!.Code.ToLowerInvariant().Insert(5, "-");

        // Act
        var first = await _redeem.Handle(Redeem(formatted, new KeyPair()), CancellationToken.None);
        var second = await _redeem.Handle(Redeem(code.Value.Code, new KeyPair()), CancellationToken.None);

        // Assert
        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Approval_Signed_By_Another_Key_Is_Rejected()
    {
        // Arrange
        var (user, approver, _) = await SaveUserWithDevice();
        var pending = await StartLink(user, approver, new KeyPair());
        var forged = new KeyPair().Sign([.. Encoding.ASCII.GetBytes("device-approval-v1"), .. new byte[64]]);

        // Act
        var result = await _approve.Handle(new ApproveLinkCommand { UserId = user.Id, DeviceId = approver.Id, PendingLinkId = pending.PendingLinkId, Approval = forged }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        Db.Device.Should().HaveCount(1);
    }

    [Fact]
    public async Task Only_The_Issuing_Device_Can_See_Or_Decide_The_Link()
    {
        // Arrange
        var (user, approver, _) = await SaveUserWithDevice();
        var other = new Device(Guid.NewGuid(), new KeyPair().PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(other);
        await Db.SaveChangesAsync();
        var pending = await StartLink(user, approver, new KeyPair());

        // Act
        var details = await _details.Handle(new GetPendingLinkQuery { UserId = user.Id, DeviceId = other.Id, PendingLinkId = pending.PendingLinkId }, CancellationToken.None);
        var reject = await _reject.Handle(new RejectLinkCommand { UserId = user.Id, DeviceId = other.Id, PendingLinkId = pending.PendingLinkId }, CancellationToken.None);

        // Assert
        details.Success.Should().BeFalse();
        reject.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Rejected_Link_Reports_Rejected_And_Cannot_Then_Be_Approved()
    {
        // Arrange
        var (user, approver, approverKey) = await SaveUserWithDevice();
        var newDevice = new KeyPair();
        var pending = await StartLink(user, approver, newDevice);

        // Act
        var rejected = await _reject.Handle(new RejectLinkCommand { UserId = user.Id, DeviceId = approver.Id, PendingLinkId = pending.PendingLinkId }, CancellationToken.None);
        var status = await _status.Handle(new GetLinkStatusQuery { PendingLinkId = pending.PendingLinkId, PollSecret = pending.PollSecret }, CancellationToken.None);
        var approval = approverKey.Sign([.. Encoding.ASCII.GetBytes("device-approval-v1"), .. Convert.FromBase64String(user.AccountPublicKey), .. Convert.FromBase64String(newDevice.PublicKey)]);
        var approved = await _approve.Handle(new ApproveLinkCommand { UserId = user.Id, DeviceId = approver.Id, PendingLinkId = pending.PendingLinkId, Approval = approval }, CancellationToken.None);

        // Assert
        rejected.Success.Should().BeTrue();
        status.Value!.Status.Should().Be("Rejected");
        approved.Success.Should().BeFalse();
        Db.Device.Should().HaveCount(1);
    }

    [Fact]
    public async Task Status_With_The_Wrong_Poll_Secret_Reveals_Nothing()
    {
        // Arrange
        var (user, approver, _) = await SaveUserWithDevice();
        var pending = await StartLink(user, approver, new KeyPair());

        // Act
        var status = await _status.Handle(new GetLinkStatusQuery { PendingLinkId = pending.PendingLinkId, PollSecret = Convert.ToBase64String(new byte[32]) }, CancellationToken.None);

        // Assert
        status.Value!.Status.Should().Be("Expired");
        status.Value.DeviceId.Should().BeNull();
    }

    private async Task<RedeemLinkCodeDto> StartLink(User user, Device approver, KeyPair newDevice)
    {
        var code = await _createCode.Handle(new CreateLinkCodeCommand { UserId = user.Id, DeviceId = approver.Id }, CancellationToken.None);
        return (await _redeem.Handle(Redeem(code.Value!.Code, newDevice), CancellationToken.None)).Value!;
    }

    private static RedeemLinkCodeCommand Redeem(string code, KeyPair device) => new()
    {
        Code = code,
        DevicePublicKey = device.PublicKey,
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Linux,
    };

    private async Task<(User, Device, KeyPair)> SaveUserWithDevice()
    {
        var user = new User(Guid.NewGuid(), new KeyPair().PublicKey);
        var key = new KeyPair();
        var device = new Device(Guid.NewGuid(), key.PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return (user, device, key);
    }

    private sealed class KeyPair
    {
        private readonly Ed25519PrivateKeyParameters _private = new(new SecureRandom());

        public string PublicKey => Convert.ToBase64String(_private.GeneratePublicKey().GetEncoded());

        public string Sign(byte[] message)
        {
            var signer = new Ed25519Signer();
            signer.Init(true, _private);
            signer.BlockUpdate(message, 0, message.Length);
            return Convert.ToBase64String(signer.GenerateSignature());
        }
    }
}
