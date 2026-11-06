using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Queries.GetPendingLink;

public sealed class GetPendingLinkQueryHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache)
    : IQueryHandler<GetPendingLinkQuery, PendingLinkDto>
{
    public async ValueTask<Result<PendingLinkDto>> Handle(GetPendingLinkQuery q, CancellationToken ct)
    {
        var pending = await deviceLinkCache.GetPendingAsync(q.PendingLinkId, ct);
        if (pending is null || !pending.IsOpenFor(q.UserId, q.DeviceId))
            return Result.Fail<PendingLinkDto>(Errors.General.NotFound(q.PendingLinkId));

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(q.UserId, excludeDeleted: true), ct);
        if (user is null)
            return Result.Fail<PendingLinkDto>(Errors.General.NotFound(q.UserId));

        return Result.Ok(new PendingLinkDto(pending.Id, pending.DevicePublicKey, pending.DevicePlatform.ToString(), user.AccountPublicKey, pending.ExpiresAt));
    }
}
