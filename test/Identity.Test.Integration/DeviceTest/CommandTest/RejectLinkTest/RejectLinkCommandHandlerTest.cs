using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Features.Device.Commands.RejectLink;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.DeviceTest.CommandTest.RejectLinkTest;

public class RejectLinkCommandHandlerTest : IntegrationTestBase
{
    private readonly IDeviceLinkCache _cache;
    private readonly RejectLinkCommandHandler _sut;

    public RejectLinkCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        _cache = factory.Services.CreateScope().ServiceProvider.GetRequiredService<IDeviceLinkCache>();
        _sut = new RejectLinkCommandHandler(_cache);
    }

    [Fact]
    public async Task Handle_Given_Open_Link_Should_Mark_It_Rejected()
    {
        // Arrange
        var pending = await AddPending();

        // Act
        var result = await _sut.Handle(new RejectLinkCommand { UserId = pending.UserId, DeviceId = pending.IssuingDeviceId, PendingLinkId = pending.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        (await _cache.GetPendingAsync(pending.Id, CancellationToken.None))!.Status.Should().Be(PendingLinkStatus.Rejected);
    }

    [Fact]
    public async Task Handle_From_Another_Device_Should_Fail()
    {
        // Arrange
        var pending = await AddPending();

        // Act
        var result = await _sut.Handle(new RejectLinkCommand { UserId = pending.UserId, DeviceId = Guid.NewGuid(), PendingLinkId = pending.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        (await _cache.GetPendingAsync(pending.Id, CancellationToken.None))!.Status.Should().Be(PendingLinkStatus.Pending);
    }

    private async Task<PendingLink> AddPending()
    {
        var pending = new PendingLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new KeyPair().PublicKey, "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));
        await _cache.SetPendingAsync(pending, CancellationToken.None);
        return pending;
    }
}
