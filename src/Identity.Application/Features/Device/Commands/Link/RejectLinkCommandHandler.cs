using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Commands.Link;

public sealed class RejectLinkCommandHandler(IDeviceLinkCache deviceLinkCache) : ICommandHandler<RejectLinkCommand>
{
    public async ValueTask<Result> Handle(RejectLinkCommand cmd, CancellationToken ct)
    {
        var pending = await PendingLinkAccess.GetOpenForIssuer(deviceLinkCache, cmd.PendingLinkId, cmd.UserId, cmd.DeviceId, ct);

        if (pending is null || await deviceLinkCache.TryClaimDecisionAsync(pending.Id, ct) is false)
            return Result.Fail(Errors.General.NotFound(cmd.PendingLinkId));

        await deviceLinkCache.SetPendingAsync(pending with { Status = PendingLinkStatus.Rejected }, ct);
        return Result.Ok();
    }
}
