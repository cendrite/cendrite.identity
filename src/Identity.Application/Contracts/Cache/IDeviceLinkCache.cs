using Identity.Application.Caching.DeviceLink;

namespace Identity.Application.Contracts.Cache;

public interface IDeviceLinkCache
{
    Task AddCodeAsync(LinkCode code, CancellationToken cancellationToken);
    Task<LinkCode?> GetCodeAsync(string code, CancellationToken cancellationToken);
    Task DeleteCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> TryClaimCodeAsync(string code, CancellationToken cancellationToken);

    Task SetPendingAsync(PendingLink pending, CancellationToken cancellationToken);
    Task<PendingLink?> GetPendingAsync(Guid pendingLinkId, CancellationToken cancellationToken);
    Task<bool> TryClaimDecisionAsync(Guid pendingLinkId, CancellationToken cancellationToken);
    Task ReleaseDecisionAsync(Guid pendingLinkId, CancellationToken cancellationToken);
}
