using FluentAssertions;
using Identity.Application.Caching.DeviceLink;

namespace Identity.Test.Unit.CachingTest;

public class LinkCodeTest
{
    [Fact]
    public void Expired_Should_Follow_ExpiresAt()
    {
        new LinkCode("ABCDE12345", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5)).Expired.Should().BeFalse();
        new LinkCode("ABCDE12345", Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1)).Expired.Should().BeTrue();
    }
}
