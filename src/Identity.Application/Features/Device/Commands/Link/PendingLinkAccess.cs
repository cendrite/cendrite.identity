using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;

namespace Identity.Application.Features.Device.Commands.Link;

internal static class PendingLinkAccess
{
    public static async Task<PendingLink?> GetOpenForIssuer(IDeviceLinkCache cache, Guid pendingLinkId, Guid userId, Guid deviceId, CancellationToken ct)
    {
        var pending = await cache.GetPendingAsync(pendingLinkId, ct);
        return pending is not null
               && !pending.Expired
               && pending.Status == PendingLinkStatus.Pending
               && pending.UserId == userId
               && pending.IssuingDeviceId == deviceId
            ? pending
            : null;
    }
}
