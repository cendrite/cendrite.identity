using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Features.Device.Commands.RejectLink;
using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.DeviceTest.CommandTest.RejectLinkTest;

public class RejectLinkCommandHandlerTest
{
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly RejectLinkCommandHandler _sut;
    private readonly PendingLink _pending = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device-key", "1.0", DeviceType.Desktop,
        DevicePlatform.Linux, "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));

    public RejectLinkCommandHandlerTest()
    {
        _sut = new RejectLinkCommandHandler(_fakeCache);

        A.CallTo(() => _fakeCache.GetPendingAsync(_pending.Id, A<CancellationToken>._)).Returns(_pending);
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(_pending.Id, A<CancellationToken>._)).Returns(true);
    }

    private RejectLinkCommand Command(Guid? deviceId = null) => new()
    {
        UserId = _pending.UserId,
        DeviceId = deviceId ?? _pending.IssuingDeviceId,
        PendingLinkId = _pending.Id,
    };

    [Fact]
    public async Task Handle_Given_Open_Link_Should_Mark_It_Rejected()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeCache.SetPendingAsync(A<PendingLink>.That.Matches(p => p.Id == _pending.Id && p.Status == PendingLinkStatus.Rejected),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_From_Another_Device_Should_Fail_Without_Claiming()
    {
        // Act
        var result = await _sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_pending.Id));
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Already_Decided_Link_Should_Fail()
    {
        // Arrange
        A.CallTo(() => _fakeCache.TryClaimDecisionAsync(_pending.Id, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        A.CallTo(() => _fakeCache.SetPendingAsync(A<PendingLink>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
