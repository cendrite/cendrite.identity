using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.CachingTest;

public class PendingLinkTest
{
    private readonly PendingLink _pending = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-key", "1.0", DeviceType.Desktop,
        DevicePlatform.Linux, "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));

    [Fact]
    public void IsOpenFor_Given_Issuing_Device_Of_Undecided_Link_Should_Be_True()
    {
        _pending.IsOpenFor(_pending.UserId, _pending.IssuingDeviceId).Should().BeTrue();
    }

    [Fact]
    public void IsOpenFor_Given_Another_User_Or_Device_Should_Be_False()
    {
        _pending.IsOpenFor(Guid.NewGuid(), _pending.IssuingDeviceId).Should().BeFalse();
        _pending.IsOpenFor(_pending.UserId, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void IsOpenFor_Given_Decided_Or_Expired_Link_Should_Be_False()
    {
        (_pending with { Status = PendingLinkStatus.Approved }).IsOpenFor(_pending.UserId, _pending.IssuingDeviceId).Should().BeFalse();
        (_pending with { Status = PendingLinkStatus.Rejected }).IsOpenFor(_pending.UserId, _pending.IssuingDeviceId).Should().BeFalse();
        (_pending with { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) }).IsOpenFor(_pending.UserId, _pending.IssuingDeviceId).Should().BeFalse();
    }
}
