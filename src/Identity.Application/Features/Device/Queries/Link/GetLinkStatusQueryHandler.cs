using System.Security.Cryptography;
using System.Text;
using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Queries.Link;

public sealed class GetLinkStatusQueryHandler(
    IDeviceLinkCache deviceLinkCache,
    IRateLimiter rateLimiter)
    : IQueryHandler<GetLinkStatusQuery, LinkStatusDto>
{
    private static readonly LinkStatusDto Expired = new("Expired", null, null, null);

    public async ValueTask<Result<LinkStatusDto>> Handle(GetLinkStatusQuery q, CancellationToken ct)
    {
        if (await rateLimiter.IsAllowedAsync($"link:poll:{q.PendingLinkId}", 60, TimeSpan.FromMinutes(1), ct) is false)
            return Result.Fail<LinkStatusDto>(Errors.General.TooManyRequests());

        var pending = await deviceLinkCache.GetPendingAsync(q.PendingLinkId, ct);
        if (pending is null || !SecretMatches(pending.PollSecretHash, q.PollSecret))
            return Result.Ok(Expired);

        return pending.Status switch
        {
            PendingLinkStatus.Approved => Result.Ok(new LinkStatusDto("Approved", pending.UserId, pending.DeviceId, pending.DeviceConnectionId)),
            PendingLinkStatus.Rejected => Result.Ok(new LinkStatusDto("Rejected", null, null, null)),
            _ when pending.Expired => Result.Ok(Expired),
            _ => Result.Ok(new LinkStatusDto("Pending", null, null, null)),
        };
    }

    private static bool SecretMatches(string expectedHash, string pollSecret)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (!Convert.TryFromBase64String(pollSecret, buffer, out var written) || written != 32)
            return false;

        var actual = Convert.ToBase64String(SHA256.HashData(buffer[..written]));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expectedHash));
    }
}
