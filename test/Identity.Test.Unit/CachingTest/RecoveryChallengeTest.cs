using System.Text.Json;
using FluentAssertions;
using Identity.Application.Caching.RecoveryChallenge;

namespace Identity.Test.Unit.CachingTest;

public class RecoveryChallengeTest
{
    [Fact]
    public void Create_Should_Bind_The_Challenge_To_The_Account_Key()
    {
        // Act
        var challenge = RecoveryChallenge.Create("account-key");

        // Assert
        challenge.AccountPublicKey.Should().Be("account-key");
        Convert.FromBase64String(challenge.ChallengeValue).Should().HaveCount(32);
        challenge.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(1));
        challenge.Expired.Should().BeFalse();
    }

    [Fact]
    public void JsonConverter_Should_Round_Trip()
    {
        // Arrange
        var options = new JsonSerializerOptions { Converters = { new RecoveryChallengeJsonConverter() } };
        var challenge = RecoveryChallenge.Create("account-key");

        // Act
        var result = JsonSerializer.Deserialize<RecoveryChallenge>(JsonSerializer.Serialize(challenge, options), options);

        // Assert
        result.Should().BeEquivalentTo(challenge);
    }

    [Fact]
    public void FromCache_Given_Empty_Value_Should_Throw()
    {
        // Act
        var act = () => RecoveryChallenge.FromCache(Guid.NewGuid(), " ", "key", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5));

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
