using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetConnectionIdByUser;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetConnectionIdsByUserTest;

public class GetConnectionIdsByUserQueryHandlerTest
{
    private readonly IUserRepository _fakeRepository;
    private readonly GetConnectionIdsByUserQueryHandler _sut;

    public GetConnectionIdsByUserQueryHandlerTest()
    {
        _fakeRepository = A.Fake<IUserRepository>();
        _sut = new GetConnectionIdsByUserQueryHandler(_fakeRepository);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var query = new GetConnectionIdsByUserQuery { TenantId = Guid.NewGuid() };
        A.CallTo(() => _fakeRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User)null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(query.TenantId));
    }

    [Fact]
    public async Task Handle_WhenUserFound_ReturnsConnectionIds()
    {
        // Arrange
        var query = new GetConnectionIdsByUserQuery { TenantId = Guid.NewGuid() };
        var user = new User();
        user.AddDevice(new Device(Guid.NewGuid(), "test", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Android, user.Id));
        A.CallTo(() => _fakeRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.ConnectionIds.Should().Contain(x => x == user.Devices.First().ConnectionId);
    }
    
    [Fact]
    public async Task Handle_When_User_Has_No_Devices_Should_Return_EmptyList()
    {
        // Arrange
        var query = new GetConnectionIdsByUserQuery { TenantId = Guid.NewGuid() };
        var user = new User();

        A.CallTo(() => _fakeRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.ConnectionIds.Should().BeEmpty();
    }
}