using FluentAssertions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Features.Authentication.Commands.BeginRecovery;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;
using Identity.Test.Integration.Setup.Helpers;

namespace Identity.Test.Integration.AuthenticationTest.CommandTest.BeginRecovery;

public class BeginRecoveryCommandHandlerTest : IntegrationTestBase
{
    private readonly BeginRecoveryCommandHandler _sut;
    private readonly IRecoveryChallengeCache _cache;

    public BeginRecoveryCommandHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var sp = factory.Services.CreateScope().ServiceProvider;
        _cache = sp.GetRequiredService<IRecoveryChallengeCache>();
        _sut = new BeginRecoveryCommandHandler(_cache, sp.GetRequiredService<IRateLimiter>());
    }

    private static string NewKey() => Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray());

    [Fact]
    public async Task Handle_Should_Store_A_Challenge_Bound_To_The_Key()
    {
        // Arrange
        var key = NewKey();

        // Act
        var result = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = key }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var stored = await _cache.GetChallengeAsync(result.Value!.ChallengeId, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.AccountPublicKey.Should().Be(key);
        stored.ChallengeValue.Should().Be(result.Value.Challenge);
    }

    [Fact]
    public async Task Handle_Should_Answer_The_Same_Way_For_Unknown_Keys()
    {
        // Arrange
        var knownKey = NewKey();
        Db.User.Add(new User(Guid.NewGuid(), knownKey, UserTags.New()));
        await Db.SaveChangesAsync();

        // Act
        var known = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = knownKey }, CancellationToken.None);
        var unknown = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = NewKey() }, CancellationToken.None);

        // Assert
        known.Success.Should().BeTrue();
        unknown.Success.Should().BeTrue();
        Convert.FromBase64String(known.Value!.Challenge).Should().HaveCount(32);
        Convert.FromBase64String(unknown.Value!.Challenge).Should().HaveCount(32);
    }

    [Fact]
    public async Task Handle_Should_Rate_Limit_Per_Account_Key()
    {
        // Arrange
        var key = NewKey();
        for (var i = 0; i < 10; i++)
            (await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = key }, CancellationToken.None)).Success.Should().BeTrue();

        // Act
        var limited = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = key }, CancellationToken.None);
        var otherKey = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = NewKey() }, CancellationToken.None);

        // Assert
        limited.Success.Should().BeFalse();
        limited.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        otherKey.Success.Should().BeTrue();
    }
}
