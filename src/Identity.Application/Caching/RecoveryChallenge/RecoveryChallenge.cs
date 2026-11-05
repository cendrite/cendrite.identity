using System.Security.Cryptography;

namespace Identity.Application.Caching.RecoveryChallenge;

public class RecoveryChallenge
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public Guid Id { get; private init; }
    public string ChallengeValue { get; private init; } = null!;
    public string AccountPublicKey { get; private init; } = null!;
    public DateTime CreatedAt { get; private init; }
    public DateTime ExpiresAt { get; private init; }
    public bool Expired => DateTime.UtcNow > ExpiresAt;

    private RecoveryChallenge() { }

    public static RecoveryChallenge Create(string accountPublicKey)
    {
        var now = DateTime.UtcNow;

        return new RecoveryChallenge
        {
            Id = Guid.NewGuid(),
            ChallengeValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            AccountPublicKey = accountPublicKey,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
        };
    }

    public static RecoveryChallenge FromCache(Guid id, string challengeValue, string accountPublicKey, DateTime createdAt, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(challengeValue))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(challengeValue));

        return new RecoveryChallenge
        {
            Id = id,
            ChallengeValue = challengeValue,
            AccountPublicKey = accountPublicKey,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }
}
