using FakeItEasy;
using FluentAssertions;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Features.Authentication.Queries.GetSignupChallenge;
using Identity.Application.Settings;
using Microsoft.Extensions.Options;

namespace Identity.Test.Unit.AuthenticationTest.QueryTest.GetSignupChallengeTest;

public class GetSignupChallengeQueryHandlerTest
{
    private readonly ISignupChallengeCache _fakeCache;
    private readonly GetSignupChallengeQueryHandler _sut;

    public GetSignupChallengeQueryHandlerTest()
    {
        _fakeCache = A.Fake<ISignupChallengeCache>();
        var settings = Options.Create(new SignupChallengeSettings { Difficulty = 16, Lifetime = TimeSpan.FromMinutes(3) });

        _sut = new GetSignupChallengeQueryHandler(_fakeCache, settings);
    }

    [Fact]
    public async Task Handle_Should_Cache_And_Return_Challenge_With_Configured_Difficulty()
    {
        // Arrange
        SignupChallenge? cached = null;
        A.CallTo(() => _fakeCache.AddChallengeAsync(A<SignupChallenge>._, A<CancellationToken>._))
            .Invokes((SignupChallenge challenge, CancellationToken _) => cached = challenge);

        // Act
        var result = await _sut.Handle(new GetSignupChallengeQuery(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Value!.Difficulty.Should().Be(16);
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(3), TimeSpan.FromSeconds(1));
        cached.Should().NotBeNull();
        result.Value.ChallengeId.Should().Be(cached!.Id);
        result.Value.Challenge.Should().Be(cached.ChallengeValue);
        A.CallTo(() => _fakeCache.AddChallengeAsync(A<SignupChallenge>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
