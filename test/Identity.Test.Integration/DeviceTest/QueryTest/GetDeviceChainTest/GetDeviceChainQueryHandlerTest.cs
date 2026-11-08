using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Queries.GetDeviceChain;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.DeviceTest.QueryTest.GetDeviceChainTest;

public class GetDeviceChainQueryHandlerTest : IntegrationTestBase
{
    private readonly GetDeviceChainQueryHandler _sut;

    public GetDeviceChainQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        _sut = new GetDeviceChainQueryHandler(factory.Services.CreateScope().ServiceProvider.GetRequiredService<IUserRepository>());
    }

    [Fact]
    public async Task Handle_Given_Connection_Should_Return_The_Owners_Stored_Chain()
    {
        // Arrange
        var oldAccount = new KeyPair();
        var user = new User(Guid.NewGuid(), oldAccount.PublicKey);
        var signal = KeyPair.NewSignalIdentityKey();
        var phone = new Device(Guid.NewGuid(), new KeyPair().PublicKey, signal, "1.0", DeviceType.Mobile, DevicePlatform.Android, user.Id, "account", "phone-approval");
        var removed = new Device(Guid.NewGuid(), new KeyPair().PublicKey, KeyPair.NewSignalIdentityKey(), "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id, phone.Id.ToString(), "old-approval");
        removed.SetDelete();
        user.AddDevice(phone);
        user.AddDevice(removed);
        user.RotateAccountKey(phone.Id, new KeyPair().PublicKey, "proof", "approval");
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(new GetDeviceChainQuery { ConnectionId = phone.ConnectionId }, CancellationToken.None);

        // Assert
        result.Value!.UserId.Should().Be(user.Id);
        result.Value.AccountPublicKey.Should().Be(user.AccountPublicKey);
        result.Value.Devices.Should().HaveCount(2);
        result.Value.Devices.Single(d => d.Id == phone.Id).SignalIdentityKey.Should().Be(signal);
        result.Value.Devices.Single(d => d.Id == removed.Id).Removed.Should().BeTrue();
        result.Value.Rotations.Should().ContainSingle(r => r.OldAccountPublicKey == oldAccount.PublicKey && r.DeviceId == phone.Id);
    }

    [Fact]
    public async Task Handle_Given_Unknown_Connection_Should_Fail()
    {
        // Act
        var result = await _sut.Handle(new GetDeviceChainQuery { ConnectionId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }
}
