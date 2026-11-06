using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.RedeemLinkCode;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.Device;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.CommandTest.RedeemLinkCodeTest;

public class RedeemLinkCodeCommandHandlerTest : IntegrationTestBase
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly IDeviceLinkCache _cache;
    private readonly RedeemLinkCodeCommandHandler _sut;

    public RedeemLinkCodeCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IDeviceLinkCache>();
        _sut = new RedeemLinkCodeCommandHandler(sp.GetRequiredService<IUserRepository>(), _cache, sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<ILogger<RedeemLinkCodeCommandHandler>>());
    }

    [Fact]
    public async Task Handle_Given_Valid_Code_Should_Create_Pending_Link_And_Raise_Event()
    {
        // Arrange
        var (user, issuer) = await SaveUserWithDevice();
        var code = await AddCode(user, issuer);
        var device = new KeyPair();

        // Act
        var result = await _sut.Handle(Command(code.Code.ToLowerInvariant().Insert(5, "-"), device), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var pending = await _cache.GetPendingAsync(result.Value!.PendingLinkId, CancellationToken.None);
        pending!.IssuingDeviceId.Should().Be(issuer.Id);
        pending.DevicePublicKey.Should().Be(device.PublicKey);
        pending.Status.Should().Be(PendingLinkStatus.Pending);
        (await _cache.GetCodeAsync(code.Code, CancellationToken.None)).Should().BeNull();
        Db.OutboxMessage.Should().Contain(m => m.Type.Contains(nameof(DeviceLinkRequestedEvent)));
    }

    [Fact]
    public async Task Handle_Given_Used_Code_Should_Fail()
    {
        // Arrange
        var (user, issuer) = await SaveUserWithDevice();
        var code = await AddCode(user, issuer);
        await _sut.Handle(Command(code.Code, new KeyPair()), CancellationToken.None);

        // Act
        var result = await _sut.Handle(Command(code.Code, new KeyPair()), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    private async Task<LinkCode> AddCode(User user, Device issuer)
    {
        var value = new string(Enumerable.Range(0, 10).Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]).ToArray());
        var code = new LinkCode(value, user.Id, issuer.Id, DateTime.UtcNow.AddMinutes(5));
        await _cache.AddCodeAsync(code, CancellationToken.None);
        return code;
    }

    private static RedeemLinkCodeCommand Command(string code, KeyPair device) => new()
    {
        Code = code,
        DevicePublicKey = device.PublicKey,
        DeviceAppVersion = "1.0",
        DeviceType = DeviceType.Desktop,
        DevicePlatform = DevicePlatform.Linux,
    };

    private async Task<(User, Device)> SaveUserWithDevice()
    {
        var user = new User(Guid.NewGuid(), new KeyPair().PublicKey);
        var device = new Device(Guid.NewGuid(), new KeyPair().PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return (user, device);
    }
}
