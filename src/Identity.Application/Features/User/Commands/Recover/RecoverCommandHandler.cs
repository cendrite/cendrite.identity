using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.RecoveryChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Commands.Recover;

public sealed class RecoverCommandHandler(
    IUserRepository userRepository,
    IRecoveryChallengeCache recoveryChallengeCache,
    IVerifyNonceService signatureVerifier,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    IChatClientService chatClientService,
    ILogger<RecoverCommandHandler> logger)
    : ICommandHandler<RecoverCommand, RecoverDto>
{
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

        var device = deviceService.RegisterDevice(user, cmd.DevicePublicKey, cmd.SignalIdentityKey, cmd.DeviceAppVersion, cmd.DeviceType, cmd.DevicePlatform, DeviceConstants.ApprovedByAccount, cmd.DeviceApproval);

        var createClientResult = await chatClientService.CreateClientAsync(user.Id, device.Id, device.ConnectionId, ct);
        if (createClientResult.Success is false)
            return Result.Fail<RecoverDto>(RecoveryFailed);

        user.AddDomainEvent(new DeviceAddedEvent(user.Id, device.Id, device.ConnectionId, device.Platform));

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
        var valid = signatureVerifier.VerifyNonce(SignedMessages.RecoveryChallenge(challenge.ChallengeValue), cmd.ChallengeSignature, cmd.AccountPublicKey)
                    && signatureVerifier.VerifyNonce(SignedMessages.DeviceApproval(cmd.AccountPublicKey, cmd.DevicePublicKey, cmd.SignalIdentityKey), cmd.DeviceApproval, cmd.AccountPublicKey);
        if (!valid)
            logger.LogWarning("Recovery challenge {ChallengeId} has an invalid signature", challenge.Id);

        return valid;
    }
}
