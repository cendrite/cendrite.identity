using System.Security.Cryptography;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Features.Device.Queries.GetLinkStatus;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.DeviceTest.QueryTest.GetLinkStatusTest;

public class GetLinkStatusQueryHandlerTest : IntegrationTestBase
{
    private readonly IDeviceLinkCache _cache;
    private readonly GetLinkStatusQueryHandler _sut;

    public GetLinkStatusQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IDeviceLinkCache>();
        _sut = new GetLinkStatusQueryHandler(_cache, sp.GetRequiredService<IRateLimiter>());
    }

    [Fact]
    public async Task Handle_Given_Approved_Link_Should_Return_The_New_Device()
    {
        // Arrange
        var secret = RandomNumberGenerator.GetBytes(32);
        var pending = await AddPending(secret, PendingLinkStatus.Approved, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _sut.Handle(new GetLinkStatusQuery { PendingLinkId = pending.Id, PollSecret = Convert.ToBase64String(secret) }, CancellationToken.None);

        // Assert
        result.Value!.Status.Should().Be("Approved");
        result.Value.UserId.Should().Be(pending.UserId);
        result.Value.DeviceId.Should().Be(pending.DeviceId);
        result.Value.DeviceConnectionId.Should().Be(pending.DeviceConnectionId);
    }

    [Fact]
    public async Task Handle_Given_Wrong_Poll_Secret_Should_Report_Expired()
    {
        // Arrange
        var pending = await AddPending(RandomNumberGenerator.GetBytes(32), PendingLinkStatus.Approved, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _sut.Handle(new GetLinkStatusQuery { PendingLinkId = pending.Id, PollSecret = Convert.ToBase64String(new byte[32]) }, CancellationToken.None);

        // Assert
        result.Value!.Status.Should().Be("Expired");
        result.Value.DeviceId.Should().BeNull();
    }

    private async Task<PendingLink> AddPending(byte[] secret, PendingLinkStatus status, Guid? deviceId, Guid? connectionId)
    {
        var pending = new PendingLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new KeyPair().PublicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            Convert.ToBase64String(SHA256.HashData(secret)), DateTime.UtcNow.AddMinutes(5), status, deviceId, connectionId);
        await _cache.SetPendingAsync(pending, CancellationToken.None);
        return pending;
    }
}
