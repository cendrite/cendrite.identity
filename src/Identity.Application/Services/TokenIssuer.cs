using System.Security.Cryptography;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Interfaces;
using Identity.Domain.Constants;
using Microsoft.Extensions.Logging;
using PrivacyPass.Issuance;

namespace Identity.Application.Services;

internal sealed class TokenIssuer(
    ITokenIssuerKeyRepository tokenIssuerKeyRepository,
    IRateLimiter rateLimiter,
    ILogger<TokenIssuer> logger)
    : ITokenIssuer
{
    private static readonly TimeSpan IssuanceWindow = TimeSpan.FromHours(PrivateTokenConstants.IssuanceWindowHours);

    public async Task<IReadOnlyList<string>> IssueAsync(Guid userId, IReadOnlyList<string> tokenRequests, CancellationToken ct = default)
    {
        if (tokenRequests.Count is 0) return [];

        try
        {
            var key = await tokenIssuerKeyRepository.GetCurrentAsync(ct);
            if (key is null)
            {
                logger.LogWarning("No token issuer key exists yet, so no tokens were issued to user {UserId}", userId);
                return [];
            }

            var parsed = new List<TokenRequest>(tokenRequests.Count);

            foreach (var encoded in tokenRequests)
            {
                if (!TryDecode(encoded, out var bytes) || !TokenRequest.TryParse(bytes, out var tokenRequest))
                {
                    logger.LogWarning("User {UserId} sent a malformed token request, so no tokens were issued", userId);
                    return [];
                }

                parsed.Add(tokenRequest!);
            }

            var granted = await rateLimiter.ConsumeUpToAsync(
                $"tokens:issue:{userId}",
                parsed.Count,
                PrivateTokenConstants.IssuanceBudget,
                IssuanceWindow,
                ct);

            if (granted < parsed.Count)
                logger.LogInformation("User {UserId} reached the token issuance budget, issuing {Granted} of {Requested}", userId, granted, parsed.Count);

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(key.PrivateKey, out _);
            var signer = new BlindSigner(rsa);

            return [.. parsed.Take(granted).Select(tokenRequest => Convert.ToBase64String(signer.Sign(tokenRequest).Encode()))];
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Token requests from user {UserId} could not be signed", userId);
            return [];
        }
    }

    public async Task<byte[]?> GetPublicKeyAsync(CancellationToken ct = default)
    {
        var key = await tokenIssuerKeyRepository.GetCurrentAsync(ct);
        return key?.PublicKey;
    }

    private static bool TryDecode(string encoded, out byte[] bytes)
    {
        var buffer = new byte[encoded.Length];
        var decoded = Convert.TryFromBase64String(encoded, buffer, out var written);
        bytes = buffer[..written];
        return decoded;
    }
}
