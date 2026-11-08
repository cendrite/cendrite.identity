using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetPendingLink;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetPendingLinkTest;

public class GetPendingLinkQueryHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly GetPendingLinkQueryHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), "account-key");
    private readonly PendingLink _pending;

    public GetPendingLinkQueryHandlerTest()
    {
        _sut = new GetPendingLinkQueryHandler(_fakeRepo, _fakeCache);
        _pending = new PendingLink(Guid.NewGuid(), _user.Id, Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.MacOS,
            "poll-secret-hash", DateTime.UtcNow.AddMinutes(5));

        A.CallTo(() => _fakeCache.GetPendingAsync(_pending.Id, A<CancellationToken>._)).Returns(_pending);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
    }

    private GetPendingLinkQuery Query(Guid? deviceId = null) => new()
    {
        UserId = _user.Id,
        DeviceId = deviceId ?? _pending.IssuingDeviceId,
        PendingLinkId = _pending.Id,
    };

    [Fact]
    public async Task Handle_Given_Issuing_Device_Should_Return_What_It_Needs_To_Approve()
    {
        // Act
        var result = await _sut.Handle(Query(), CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new PendingLinkDto(_pending.Id, "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "MacOS", "account-key", _pending.ExpiresAt));
    }

    [Fact]
    public async Task Handle_From_Another_Device_Should_Fail_Without_Looking_Up_The_User()
    {
        // Act
        var result = await _sut.Handle(Query(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(_pending.Id));
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
