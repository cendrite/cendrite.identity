using FluentAssertions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.CreateLinkCode;
using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.DeviceTest.CommandTest.CreateLinkCodeTest;

public class CreateLinkCodeCommandHandlerTest : IntegrationTestBase
{
    private readonly IDeviceLinkCache _cache;
    private readonly CreateLinkCodeCommandHandler _sut;

    public CreateLinkCodeCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IDeviceLinkCache>();
        _sut = new CreateLinkCodeCommandHandler(sp.GetRequiredService<IUserRepository>(), _cache, sp.GetRequiredService<IRateLimiter>());
    }

    [Fact]
    public async Task Handle_Given_Own_Device_Should_Store_A_Code_For_It()
    {
        // Arrange
        var (user, device) = await SaveUserWithDevice();

        // Act
        var result = await _sut.Handle(new CreateLinkCodeCommand { UserId = user.Id, DeviceId = device.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Code.Should().HaveLength(10);
        var stored = await _cache.GetCodeAsync(result.Value.Code, CancellationToken.None);
        stored!.UserId.Should().Be(user.Id);
        stored.IssuingDeviceId.Should().Be(device.Id);
    }

    [Fact]
    public async Task Handle_Given_Device_Of_Another_User_Should_Fail()
    {
        // Arrange
        var (user, _) = await SaveUserWithDevice();
        var (_, otherDevice) = await SaveUserWithDevice();

        // Act
        var result = await _sut.Handle(new CreateLinkCodeCommand { UserId = user.Id, DeviceId = otherDevice.Id }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }

    private async Task<(User, Device)> SaveUserWithDevice()
    {
        var user = new User(Guid.NewGuid(), new KeyPair().PublicKey, UserTags.New());
        var device = new Device(Guid.NewGuid(), new KeyPair().PublicKey, "BQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "1.0", DeviceType.Desktop, DevicePlatform.Linux, user.Id);
        user.AddDevice(device);
        Db.User.Add(user);
        await Db.SaveChangesAsync();
        return (user, device);
    }
}
