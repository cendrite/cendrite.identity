using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Device.Commands.Link;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Queries.Link;

public sealed class GetPendingLinkQueryHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache)
    : IQueryHandler<GetPendingLinkQuery, PendingLinkDto>
{
    public async ValueTask<Result<PendingLinkDto>> Handle(GetPendingLinkQuery q, CancellationToken ct)
    {
        var pending = await PendingLinkAccess.GetOpenForIssuer(deviceLinkCache, q.PendingLinkId, q.UserId, q.DeviceId, ct);
        if (pending is null)
            return Result.Fail<PendingLinkDto>(Errors.General.NotFound(q.PendingLinkId));

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(q.UserId, excludeDeleted: true), ct);
        if (user is null)
            return Result.Fail<PendingLinkDto>(Errors.General.NotFound(q.UserId));

        return Result.Ok(new PendingLinkDto(pending.Id, pending.DevicePublicKey, pending.DevicePlatform.ToString(), user.AccountPublicKey, pending.ExpiresAt));
    }
}
