using System.Text;
using Cendrite.Domain.Common;
using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Responses.Client;
using Identity.Application.Abstractions;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Commands.Recover;

public sealed class RecoverCommandHandler(
    IUserRepository userRepository,
    IRecoveryChallengeCache recoveryChallengeCache,
    IVerifyNonceService signatureVerifier,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    IRequestClient<CreateClientMessage> clientClient,
    ILogger<RecoverCommandHandler> logger)
    : ICommandHandler<RecoverCommand, RecoverDto>
{
    private static readonly byte[] RecoveryContext = Encoding.ASCII.GetBytes("cendrite-recovery-v1");
    private static readonly byte[] DeviceApprovalContext = Encoding.ASCII.GetBytes("device-approval-v1");
    private static readonly Error RecoveryFailed = Errors.General.UnspecifiedError("Recovery failed");

    public async ValueTask<Result<RecoverDto>> Handle(RecoverCommand cmd, CancellationToken ct)
    {
        var challenge = await ClaimChallenge(cmd, ct);

        if (challenge is null || !AreSignaturesValid(challenge, cmd))
            return Result.Fail<RecoverDto>(RecoveryFailed);

        var user = await userRepository.GetSinleAsync(new UserByAccountKeySpec(cmd.AccountPublicKey, excludeDeleted: true), ct);
        if (user is null)
        {
            logger.LogWarning("Recovery challenge {ChallengeId} was signed for an account key without an account", cmd.ChallengeId);
            return Result.Fail<RecoverDto>(RecoveryFailed);
        }

        var device = deviceService.RegisterDevice(user, cmd.DevicePublicKey, cmd.DeviceAppVersion, cmd.DeviceType, cmd.DevicePlatform, DeviceConstants.ApprovedByAccount, cmd.DeviceApproval);

        var createClientResult = await CreateClient(device.Id, device.ConnectionId, user.Id);
        if (createClientResult.Success is false)
            return Result.Fail<RecoverDto>(RecoveryFailed);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Ok(new RecoverDto(user.Id, device.Id, device.ConnectionId));
    }

    private async Task<RecoveryChallenge?> ClaimChallenge(RecoverCommand cmd, CancellationToken ct)
    {
        var challenge = await recoveryChallengeCache.GetChallengeAsync(cmd.ChallengeId, ct);
        if (challenge is null || challenge.Expired || challenge.AccountPublicKey != cmd.AccountPublicKey)
        {
            logger.LogWarning("Recovery challenge {ChallengeId} not found, expired or issued for another key", cmd.ChallengeId);
            return null;
        }

        if (await recoveryChallengeCache.TryClaimChallengeAsync(cmd.ChallengeId, ct) is false)
        {
            logger.LogWarning("Recovery challenge {ChallengeId} was already used", cmd.ChallengeId);
            return null;
        }

        await recoveryChallengeCache.DeleteChallengeAsync(cmd.ChallengeId, ct);
        return challenge;
    }

    private bool AreSignaturesValid(RecoveryChallenge challenge, RecoverCommand cmd)
    {
        var challengeBytes = Convert.FromBase64String(challenge.ChallengeValue);
        var accountKey = Convert.FromBase64String(cmd.AccountPublicKey);
        var deviceKey = Convert.FromBase64String(cmd.DevicePublicKey);

        var valid = signatureVerifier.VerifyNonce(Concat(RecoveryContext, challengeBytes), cmd.ChallengeSignature, cmd.AccountPublicKey)
                    && signatureVerifier.VerifyNonce(Concat(DeviceApprovalContext, accountKey, deviceKey), cmd.DeviceApproval, cmd.AccountPublicKey);
        if (!valid)
            logger.LogWarning("Recovery challenge {ChallengeId} has an invalid signature", challenge.Id);

        return valid;
    }

    private static string Concat(params byte[][] parts)
    {
        return Convert.ToBase64String(parts.SelectMany(p => p).ToArray());
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
