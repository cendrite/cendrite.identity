using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetDeviceChain;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetDeviceChainTest;

public class GetDeviceChainQueryHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly GetDeviceChainQueryHandler _sut;

    public GetDeviceChainQueryHandlerTest()
    {
        _sut = new GetDeviceChainQueryHandler(_fakeRepo);
    }

    [Fact]
    public async Task Handle_Should_Return_Every_Device_Including_Removed_Ones_And_The_Rotations()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "key-a", UserTags.New());
        var phone = new Device(Guid.NewGuid(), "phone-key", "phone-signal", "1.0", DeviceType.Mobile, DevicePlatform.Android, user.Id, "account", "phone-approval");
        var laptop = new Device(Guid.NewGuid(), "laptop-key", "laptop-signal", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id, phone.Id.ToString(), "laptop-approval");
        phone.SetDelete();
        user.AddDevice(phone);
        user.AddDevice(laptop);
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByConnectionIdWithChainSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(new GetDeviceChainQuery { ConnectionId = laptop.ConnectionId }, CancellationToken.None);

        // Assert
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.AccountPublicKey.Should().Be("key-a");
        result.Value.Devices.Should().BeEquivalentTo(new[]
        {
            new ChainDeviceDto(phone.Id, phone.ConnectionId, "phone-key", "phone-signal", "account", "phone-approval", true),
            new ChainDeviceDto(laptop.Id, laptop.ConnectionId, "laptop-key", "laptop-signal", phone.Id.ToString(), "laptop-approval", false),
        });
    }

    [Fact]
    public async Task Handle_Given_Unknown_Connection_Should_Return_NotFound()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserByConnectionIdWithChainSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new GetDeviceChainQuery { ConnectionId = connectionId }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(connectionId));
    }
}
