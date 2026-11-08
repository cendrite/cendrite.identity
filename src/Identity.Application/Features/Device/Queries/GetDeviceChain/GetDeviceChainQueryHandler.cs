using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed class GetDeviceChainQueryHandler(IUserRepository userRepository) : IQueryHandler<GetDeviceChainQuery, DeviceChainDto>
{
    public async ValueTask<Result<DeviceChainDto>> Handle(GetDeviceChainQuery q, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserByConnectionIdWithChainSpec(q.ConnectionId), ct);
        if (user is null)
            return Result.Fail<DeviceChainDto>(Errors.General.NotFound(q.ConnectionId));

        var devices = user.GetDevices(excludeDeleted: false)
            .Select(d => new ChainDeviceDto(d.Id, d.ConnectionId, d.PublicKey, d.SignalIdentityKey, d.ApprovedBy, d.ApprovalSignature, d.Deleted is not null))
            .ToList();

        var rotations = user.AccountKeyRotations
            .OrderBy(r => r.Created)
            .Select(r => new ChainRotationDto(r.DeviceId, r.OldAccountPublicKey, r.NewAccountPublicKey, r.Proof, r.Approval, r.Created))
            .ToList();

        return Result.Ok(new DeviceChainDto(user.Id, user.AccountPublicKey, devices, rotations));
    }
}
