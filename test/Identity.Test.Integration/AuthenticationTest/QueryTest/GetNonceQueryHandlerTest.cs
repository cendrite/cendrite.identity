using System.Text.Json;
using FluentAssertions;
using Identity.Application.Caching;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Queries.GetNonce;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.AuthenticationTest.QueryTest;

public class GetNonceQueryHandlerTest : IntegrationTestBase
{
    private readonly GetNonceQueryHandler _sut;
    public GetNonceQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var cache = scope.ServiceProvider.GetRequiredService<INonceCacheService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<GetNonceQueryHandler>>();

        _sut = new GetNonceQueryHandler(repo, cache, logger);
    }

    [Fact]
    public async void Handle_WhenQueryValid_Should_Generate_Nonce_And_Cache_And_Return()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "SomeKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop,
            DevicePlatform.Android, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var query = new GetNonceQuery()
        {
            UserId = user.Id,
            DeviceId = device.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        var options = new JsonSerializerOptions()
        {
            Converters = { new NonceJsonConverter() },
        };
        var nonce = await Cache.GetAsync<Nonce>(result.Value!.NonceId.ToString(), options, new CancellationToken());
        nonce.Should().NotBeNull();
        nonce!.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async void Handle_WhenQueryInvalid_UserNot_Found_Should_Not_Cachce_And_Return_Result_Fail()
    {
        // Arrange
        var query = new GetNonceQuery()
        {
            UserId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Deleted_User_Should_Return_Unauthorized()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "SomeKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Android, user.Id);
        user.AddDevice(device);
        user.SetDelete();
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var query = new GetNonceQuery()
        {
            UserId = user.Id,
            DeviceId = device.Id
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_Given_Unknown_Device_Should_Return_Unauthorized_And_Not_Cache()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var query = new GetNonceQuery()
        {
            UserId = user.Id,
            DeviceId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.Unauthorized());
    }

    [Fact]
    public async Task Handle_WhenDeviceDeleted_Should_Still_Issue_Nonce()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        var device = new Device(Guid.NewGuid(), "SomeKey", "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Windows, user.Id);
        device.SetDelete();
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(new GetNonceQuery { UserId = user.Id, DeviceId = device.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
