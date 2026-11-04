using System.Text.Json;
using FluentAssertions;
using Identity.Application.Caching.SignupChallenge;

namespace Identity.Test.Unit.CachingTest;

public class SignupChallengeTest
{
    [Fact]
    public void Create_Should_Return_Challenge_With_Difficulty_And_Lifetime()
    {
        // Act
        var challenge = SignupChallenge.Create(20, TimeSpan.FromMinutes(5));

        // Assert
        challenge.Id.Should().NotBeEmpty();
        challenge.Difficulty.Should().Be(20);
        Convert.FromBase64String(challenge.ChallengeValue).Should().HaveCount(32);
        challenge.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        challenge.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(1));
        challenge.Expired.Should().BeFalse();
    }

    [Fact]
    public void Create_Should_Return_Unique_Challenges()
    {
        // Act
        var first = SignupChallenge.Create(20, TimeSpan.FromMinutes(5));
        var second = SignupChallenge.Create(20, TimeSpan.FromMinutes(5));

        // Assert
        first.Id.Should().NotBe(second.Id);
        first.ChallengeValue.Should().NotBe(second.ChallengeValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(257)]
    public void Create_Given_Invalid_Difficulty_Should_Throw(int difficulty)
    {
        // Act
        var act = () => SignupChallenge.Create(difficulty, TimeSpan.FromMinutes(5));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_Given_Non_Positive_Lifetime_Should_Throw()
    {
        // Act
        var act = () => SignupChallenge.Create(20, TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromCache_Given_Expired_Values_Should_Be_Expired()
    {
        // Act
        var challenge = SignupChallenge.FromCache(Guid.NewGuid(), "value", 20, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5));

        // Assert
        challenge.Expired.Should().BeTrue();
    }

    [Fact]
    public void FromCache_Given_Empty_Value_Should_Throw()
    {
        // Act
        var act = () => SignupChallenge.FromCache(Guid.NewGuid(), " ", 20, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JsonConverter_Should_Round_Trip()
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new SignupChallengeJsonConverter() } };
        var challenge = SignupChallenge.Create(18, TimeSpan.FromMinutes(5));

        // Act
        var json = JsonSerializer.Serialize(challenge, options);
        var result = JsonSerializer.Deserialize<SignupChallenge>(json, options);

        // Assert
        result.Should().BeEquivalentTo(challenge);
    }
}
