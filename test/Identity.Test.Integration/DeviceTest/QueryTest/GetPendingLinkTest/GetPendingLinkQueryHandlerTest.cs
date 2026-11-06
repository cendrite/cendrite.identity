using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetPendingLink;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.DeviceTest.QueryTest.GetPendingLinkTest;

public class GetPendingLinkQueryHandlerTest : IntegrationTestBase
{
    private readonly IDeviceLinkCache _cache;
    private readonly GetPendingLinkQueryHandler _sut;

    public GetPendingLinkQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IDeviceLinkCache>();
        _sut = new GetPendingLinkQueryHandler(sp.GetRequiredService<IUserRepository>(), _cache);
    }

    [Fact]
    public async Task Handle_Given_Issuing_Device_Should_Return_Link_Details()
    {
        // Arrange
        var (user, issuer) = await SaveUserWithDevice();
        var pending = await AddPending(user, issuer);

        // Act
        var result = await _sut.Handle(new GetPendingLinkQuery { UserId = user.Id, DeviceId = issuer.Id, PendingLinkId = pending.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.DevicePublicKey.Should().Be(pending.DevicePublicKey);
        result.Value.DevicePlatform.Should().Be(nameof(DevicePlatform.Linux));
        result.Value.AccountPublicKey.Should().Be(user.AccountPublicKey);
    }

    [Fact]
    public async Task Handle_From_Another_Device_Should_Fail()
    {
        // Arrange
        var (user, issuer) = await SaveUserWithDevice();
        var pending = await AddPending(user, issuer);

        // Act
        var result = await _sut.Handle(new GetPendingLinkQuery { UserId = user.Id, DeviceId = Guid.NewGuid(), PendingLinkId = pending.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    private async Task<PendingLink> AddPending(User user, Device issuer)
    {
        var pending = new PendingLink(Guid.NewGuid(), user.Id, issuer.Id, new KeyPair().PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));
        await _cache.SetPendingAsync(pending, CancellationToken.None);
        return pending;
    }

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
