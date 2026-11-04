using System.Security.Cryptography;

namespace Identity.Application.Caching.SignupChallenge;

public class SignupChallenge
{
    public Guid Id { get; private init; }
    public string ChallengeValue { get; private init; } = null!;
    public int Difficulty { get; private init; }
    public DateTime CreatedAt { get; private init; }
    public DateTime ExpiresAt { get; private init; }
    public bool Expired => DateTime.UtcNow > ExpiresAt;

    private SignupChallenge() { }

    public static SignupChallenge Create(int difficulty, TimeSpan lifetime)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(difficulty);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(difficulty, 256);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        var now = DateTime.UtcNow;

        return new SignupChallenge
        {
            Id = Guid.NewGuid(),
            ChallengeValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            Difficulty = difficulty,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        };
    }

    public static SignupChallenge FromCache(Guid id, string challengeValue, int difficulty, DateTime createdAt, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(challengeValue))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(challengeValue));

        return new SignupChallenge
        {
            Id = id,
            ChallengeValue = challengeValue,
            Difficulty = difficulty,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }
}
