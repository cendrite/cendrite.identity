using System.Text;
using Cendrite.Domain.Common;
using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Responses.Client;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Domain.Common;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Commands.ApproveLink;

public sealed class ApproveLinkCommandHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache,
    IVerifyNonceService signatureVerifier,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    IRequestClient<CreateClientMessage> clientClient,
    ILogger<ApproveLinkCommandHandler> logger)
    : ICommandHandler<ApproveLinkCommand>
{
    private static readonly byte[] DeviceApprovalContext = Encoding.ASCII.GetBytes("device-approval-v1");

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

        var createClientResult = await CreateClient(device.Id, device.ConnectionId, user.Id);
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
        byte[] message = [.. DeviceApprovalContext, .. Convert.FromBase64String(accountPublicKey), .. Convert.FromBase64String(devicePublicKey)];
        return signatureVerifier.VerifyNonce(Convert.ToBase64String(message), approval, approverPublicKey);
    }

    private async ValueTask<Result> CreateClient(Guid deviceId, Guid connectionId, Guid userId)
    {
        var response = await clientClient.GetResponse<CreateClientResponse>(new CreateClientMessage
        {
            CorrelationId = Guid.NewGuid(),
            DeviceId = deviceId,
            ConnectionId = connectionId,
            UserId = userId
        });

        if (response.Message.IsSuccess)
            return Result.Ok();

        logger.LogError("Got a fail response from the Chat server attempting to create a Client for Device with ID {DeviceId}", deviceId);
        return Result.Fail(Errors.General.UnspecifiedError("Failed to create client"));
    }
}
