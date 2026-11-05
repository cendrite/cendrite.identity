using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Features.Authentication.Commands.BeginRecovery;
using Identity.Domain.Common;

namespace Identity.Test.Unit.AuthenticationTest.CommandTest.BeginRecovery;

public class BeginRecoveryCommandHandlerTest
{
    private readonly IRecoveryChallengeCache _fakeCache = A.Fake<IRecoveryChallengeCache>();
    private readonly IRateLimiter _fakeRateLimiter = A.Fake<IRateLimiter>();
    private readonly BeginRecoveryCommandHandler _sut;

    public BeginRecoveryCommandHandlerTest()
    {
        _sut = new BeginRecoveryCommandHandler(_fakeCache, _fakeRateLimiter);
    }

    [Fact]
    public async Task Handle_When_Allowed_Should_Cache_A_Challenge_Bound_To_The_Key()
    {
        // Arrange
        RecoveryChallenge? cached = null;
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync("recovery:begin:key", 10, TimeSpan.FromHours(1), A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _fakeCache.AddChallengeAsync(A<RecoveryChallenge>._, A<CancellationToken>._))
            .Invokes((RecoveryChallenge challenge, CancellationToken _) => cached = challenge);

        // Act
        var result = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = "key" }, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        cached.Should().NotBeNull();
        cached!.AccountPublicKey.Should().Be("key");
        result.Value!.ChallengeId.Should().Be(cached.Id);
        result.Value.Challenge.Should().Be(cached.ChallengeValue);
        result.Value.ExpiresAt.Should().Be(cached.ExpiresAt);
    }

    [Fact]
    public async Task Handle_When_Rate_Limited_Should_Fail_Without_Creating_A_Challenge()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.IsAllowedAsync(A<string>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(false);

        // Act
        var result = await _sut.Handle(new BeginRecoveryCommand { AccountPublicKey = "key" }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.TooManyRequests());
        A.CallTo(() => _fakeCache.AddChallengeAsync(A<RecoveryChallenge>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
