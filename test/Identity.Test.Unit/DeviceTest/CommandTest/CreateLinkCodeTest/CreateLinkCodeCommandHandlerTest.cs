using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.CreateLinkCode;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Test.Unit.DeviceTest.CommandTest.CreateLinkCodeTest;

public class CreateLinkCodeCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly IDeviceLinkCache _fakeCache = A.Fake<IDeviceLinkCache>();
    private readonly IRateLimiter _fakeRateLimiter = A.Fake<IRateLimiter>();
    private readonly CreateLinkCodeCommandHandler _sut;

    private readonly User _user = new(Guid.NewGuid(), "account-key");
    private readonly Device _device;

    public CreateLinkCodeCommandHandlerTest()
    {
        _sut = new CreateLinkCodeCommandHandler(_fakeRepo, _fakeCache, _fakeRateLimiter);

        _device = new Device(Guid.NewGuid(), "device-key", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, _user.Id);
        _user.AddDevice(_device);

        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(_user);
    }

    private CreateLinkCodeCommand Command(Guid? deviceId = null) => new() { UserId = _user.Id, DeviceId = deviceId ?? _device.Id };

    [Fact]
    public async Task Handle_Given_Own_Device_Should_Store_A_Code_Issued_By_It()
    {
        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Code.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{10}$");
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
        A.CallTo(() => _fakeCache.AddCodeAsync(
                A<LinkCode>.That.Matches(c => c.Code == result.Value.Code && c.UserId == _user.Id && c.IssuingDeviceId == _device.Id),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_When_Rate_Limited_Should_Fail_Without_Looking_Up_The_User()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(Command(), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_Given_Device_Not_On_The_Account_Should_Fail()
    {
        // Act
        var result = await _sut.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
        A.CallTo(() => _fakeCache.AddCodeAsync(A<LinkCode>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
