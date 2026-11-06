using System.Security.Cryptography;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Features.Device.Queries.GetLinkStatus;
using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetLinkStatusTest;

public class GetLinkStatusQueryHandlerTest
{
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly IRateLimiter _fakeRateLimiter = A.Fake<IRateLimiter>();
    private readonly GetLinkStatusQueryHandler _sut;

    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);
    private readonly PendingLink _pending;

    public GetLinkStatusQueryHandlerTest()
    {
        _sut = new GetLinkStatusQueryHandler(_fakeCache, _fakeRateLimiter);
        _pending = new PendingLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-key", "1.0", DeviceType.Desktop, DevicePlatform.Linux,
            Convert.ToBase64String(SHA256.HashData(_secret)), DateTime.UtcNow.AddMinutes(5));

        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(true);
        Store(_pending);
    }

    private void Store(PendingLink pending) =>
        A.CallTo(() => _fakeCache.GetPendingAsync(_pending.Id, A<CancellationToken>._)).Returns(pending);

    private GetLinkStatusQuery Query(string? secret = null) => new()
    {
        PendingLinkId = _pending.Id,
        PollSecret = secret ?? Convert.ToBase64String(_secret),
    };

    [Fact]
    public async Task Handle_Given_Undecided_Link_Should_Report_Pending()
    {
        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new LinkStatusDto("Pending", null, null, null));
    }

    [Fact]
    public async Task Handle_Given_Approved_Link_Should_Return_The_New_Device()
    {
        // Arrange
        var approved = _pending with { Status = PendingLinkStatus.Approved, DeviceId = Guid.NewGuid(), DeviceConnectionId = Guid.NewGuid() };
        Store(approved);

        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new LinkStatusDto("Approved", approved.UserId, approved.DeviceId, approved.DeviceConnectionId));
    }

    [Fact]
    public async Task Handle_Given_Rejected_Link_Should_Report_Rejected()
    {
        // Arrange
        Store(_pending with { Status = PendingLinkStatus.Rejected });

        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Value!.Status.Should().Be("Rejected");
    }

    [Fact]
    public async Task Handle_Given_Expired_Link_Should_Report_Expired()
    {
        // Arrange
        Store(_pending with { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) });

        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Value!.Status.Should().Be("Expired");
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public async Task Handle_Given_Wrong_Poll_Secret_Should_Report_Expired_Even_When_Approved(string secret)
    {
        // Arrange
        Store(_pending with { Status = PendingLinkStatus.Approved, DeviceId = Guid.NewGuid(), DeviceConnectionId = Guid.NewGuid() });

        // Act
        var result = await _sut.Handle(Query(secret), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new LinkStatusDto("Expired", null, null, null));
    }

    [Fact]
    public async Task Handle_When_Rate_Limited_Should_Fail_Without_Reading_The_Cache()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        A.CallTo(() => _fakeCache.GetPendingAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
