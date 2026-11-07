using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Common;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Commands.ApproveLink;

public sealed class ApproveLinkCommandHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache,
    IVerifyNonceService signatureVerifier,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    IChatClientService chatClientService,
    ILogger<ApproveLinkCommandHandler> logger)
    : ICommandHandler<ApproveLinkCommand>
{
    public async ValueTask<Result> Handle(ApproveLinkCommand cmd, CancellationToken ct)
    {
        var pending = await deviceLinkCache.GetPendingAsync(cmd.PendingLinkId, ct);
        if (pending is null || !pending.IsOpenFor(cmd.UserId, cmd.DeviceId))
            return Result.Fail(Errors.General.NotFound(cmd.PendingLinkId));

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(cmd.UserId, excludeDeleted: true), ct);
        var approver = user?.GetDevices().FirstOrDefault(d => d.Id == cmd.DeviceId);
        if (user is null || approver is null)
            return Result.Fail(Errors.General.Unauthorized());

        if (!IsApprovalValid(user.AccountPublicKey, pending.DevicePublicKey, cmd.Approval, approver.PublicKey))
        {
            logger.LogWarning("Device {DeviceId} sent an invalid approval for pending link {PendingLinkId}", approver.Id, pending.Id);
            return Result.Fail(Errors.General.Unauthorized());
        }

        if (await deviceLinkCache.TryClaimDecisionAsync(pending.Id, ct) is false)
            return Result.Fail(Errors.General.NotFound(cmd.PendingLinkId));

        var device = deviceService.RegisterDevice(user, pending.DevicePublicKey, pending.DeviceAppVersion, pending.DeviceType, pending.DevicePlatform, approver.Id.ToString(), cmd.Approval);

        var createClientResult = await chatClientService.CreateClientAsync(user.Id, device.Id, device.ConnectionId, ct);
        if (createClientResult.Success is false)
        {
            await deviceLinkCache.ReleaseDecisionAsync(pending.Id, ct);
            return createClientResult;
        }

        user.AddDomainEvent(new DeviceAddedEvent(user.Id, device.Id, device.ConnectionId, device.Platform));
        await unitOfWork.SaveChangesAsync(ct);

        await deviceLinkCache.SetPendingAsync(pending with
        {
            Status = PendingLinkStatus.Approved,
            DeviceId = device.Id,
            DeviceConnectionId = device.ConnectionId,
        }, ct);

        return Result.Ok();
    }

    private bool IsApprovalValid(string accountPublicKey, string devicePublicKey, string approval, string approverPublicKey)
    {
        return signatureVerifier.VerifyNonce(SignedMessages.DeviceApproval(accountPublicKey, devicePublicKey), approval, approverPublicKey);
    }
}
