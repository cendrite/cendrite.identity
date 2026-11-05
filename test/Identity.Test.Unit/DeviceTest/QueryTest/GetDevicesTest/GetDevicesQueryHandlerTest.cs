using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetDevices;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.DeviceTest.QueryTest.GetDevicesTest;

public class GetDevicesQueryHandlerTest
{
    private readonly IUserRepository _fakeUserRepository;
    private readonly GetDevicesQueryHandler _sut;

    public GetDevicesQueryHandlerTest()
    {
        _fakeUserRepository = A.Fake<IUserRepository>();
        var logger = A.Fake<ILogger<GetDevicesQueryHandler>>();
        _sut = new GetDevicesQueryHandler(_fakeUserRepository, logger);
    }

    [Fact]
    public async Task Handle_GivenADevice_ShouldReturnAListOfDevices()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var device = new Device(Guid.NewGuid(), "someKey", "1.0", DeviceType.Desktop, DevicePlatform.Android, user.Id);

        user.AddDevice(device);

        var query = new GetDevicesQuery()
        {
            UserId = user.Id
        };

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Devices.Should().NotBeNull().And.NotBeEmpty();
        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_GivenNoDevices_Should_Return_Empty_Result()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var query = new GetDevicesQuery()
        {
            UserId = user.Id
        };

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Devices.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_Given_Invalid_UserId_Should_Return_Fail()
    {
        // Arrange
        var query = new GetDevicesQuery()
        {
            UserId = Guid.NewGuid()
        };

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User)null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull().And.BeEquivalentTo(Errors.General.NotFound(query.UserId));
    }
}