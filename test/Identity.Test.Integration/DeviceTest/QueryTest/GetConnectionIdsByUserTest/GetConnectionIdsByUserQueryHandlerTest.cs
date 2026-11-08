using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetConnectionIdByUser;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.DeviceTest.QueryTest.GetConnectionIdsByUserTest;

public class GetConnectionIdsByUserQueryHandlerTest : IntegrationTestBase
{
    private readonly GetConnectionIdsByUserQueryHandler _sut;
    public GetConnectionIdsByUserQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        _sut = new GetConnectionIdsByUserQueryHandler(repo);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_Should_Return_ResultFail()
    {
        // Arrange
        var query = new GetConnectionIdsByUserQuery()
        {
            TenantId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_When_User_Has_No_Devices_Should_Return_Empty_List()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var query = new GetConnectionIdsByUserQuery()
        {
            TenantId = user.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.ConnectionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_When_User_Has_Devices_Should_Return_ConnectionIds()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "somekey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Android, user.Id);
        var device2 = new Device(Guid.NewGuid(), "somekey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Android, user.Id);

        user.AddDevice(device);
        user.AddDevice(device2);
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var query = new GetConnectionIdsByUserQuery()
        {
            TenantId = user.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Arrange
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ConnectionIds.Should().NotBeEmpty().And.HaveCount(2);
    }
}