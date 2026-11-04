using System.Net;
using System.Text.Json;
using FluentAssertions;
using Identity.Application.Contracts.Cache;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.AuthenticationTest.EndpointTest;

public class SignupChallengeEndpointTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : IntegrationTestBase(factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly IServiceProvider _services = factory.Services;

    [Fact]
    public async Task GetSignupChallenge_Should_Return_Challenge_And_Store_It()
    {
        // Act
        var response = await _client.GetAsync("/api/authentication/signup-challenge");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var challengeId = body.RootElement.GetProperty("challengeId").GetGuid();
        var challenge = body.RootElement.GetProperty("challenge").GetString();
        var difficulty = body.RootElement.GetProperty("difficulty").GetInt32();

        Convert.FromBase64String(challenge!).Should().HaveCount(32);
        difficulty.Should().Be(20);

        using var scope = _services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ISignupChallengeCache>();
        var stored = await cache.GetChallengeAsync(challengeId, CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.ChallengeValue.Should().Be(challenge);
        stored.Difficulty.Should().Be(difficulty);
    }

    [Fact]
    public async Task GetSignupChallenge_Should_Return_A_New_Challenge_Each_Time()
    {
        // Act
        var first = await _client.GetStringAsync("/api/authentication/signup-challenge");
        var second = await _client.GetStringAsync("/api/authentication/signup-challenge");

        // Assert
        using var firstBody = JsonDocument.Parse(first);
        using var secondBody = JsonDocument.Parse(second);
        firstBody.RootElement.GetProperty("challengeId").GetGuid()
            .Should().NotBe(secondBody.RootElement.GetProperty("challengeId").GetGuid());
    }

    [Fact]
    public async Task Stored_Challenge_Can_Only_Be_Claimed_Once()
    {
        // Arrange
        using var body = JsonDocument.Parse(await _client.GetStringAsync("/api/authentication/signup-challenge"));
        var challengeId = body.RootElement.GetProperty("challengeId").GetGuid();

        using var scope = _services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ISignupChallengeCache>();

        // Act
        var firstClaim = await cache.TryClaimChallengeAsync(challengeId, CancellationToken.None);
        var secondClaim = await cache.TryClaimChallengeAsync(challengeId, CancellationToken.None);

        // Assert
        firstClaim.Should().BeTrue();
        secondClaim.Should().BeFalse();
    }
}
