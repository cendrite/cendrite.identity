using FluentAssertions;
using Identity.Application.Features.Device.Queries.GetDeviceChain;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetDeviceChainTest;

public class GetDeviceChainQueryValidatorTest
{
    private readonly GetDeviceChainQueryValidator _sut = new();

    [Fact]
    public void Validate_Given_ConnectionId_Should_Pass()
    {
        _sut.Validate(new GetDeviceChainQuery { ConnectionId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Empty_ConnectionId_Should_Fail()
    {
        _sut.Validate(new GetDeviceChainQuery { ConnectionId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
