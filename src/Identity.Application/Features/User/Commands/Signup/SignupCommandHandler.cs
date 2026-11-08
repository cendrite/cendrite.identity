using Cendrite.Domain.Common;
using Cendrite.Message.Contracts.Messages.Profile;
using Cendrite.Message.Contracts.Responses.Profile;
using Identity.Application.Abstractions;
using Identity.Application.Caching.SignupChallenge;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Common;
using Identity.Domain.Constants;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Commands.Signup;

public sealed class SignupCommandHandler(
    IUserRepository userRepository,
    ISignupChallengeCache signupChallengeCache,
    IProofOfWorkService proofOfWorkService,
    IVerifyNonceService signatureVerifier,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    IRequestClient<CreateUserProfileMessage> profileClient,
    IChatClientService chatClientService,
    ILogger<SignupCommandHandler> logger)
    : ICommandHandler<SignupCommand, SignupDto>
{
    private static readonly Error SignupFailed = Errors.General.UnspecifiedError("Signup failed");

    public async ValueTask<Result<SignupDto>> Handle(SignupCommand cmd, CancellationToken ct)
    {
        var challenge = await ClaimChallenge(cmd.ChallengeId, ct);

        if (challenge is null
            || !IsProofOfWorkValid(challenge, cmd)
            || !AreSignaturesValid(challenge, cmd)
            || await AccountExists(cmd, ct))
            return Result.Fail<SignupDto>(SignupFailed);

        var user = new Domain.Aggregates.User(Guid.NewGuid(), cmd.AccountPublicKey);
        var device = deviceService.RegisterDevice(user, cmd.DevicePublicKey, cmd.SignalIdentityKey, cmd.DeviceAppVersion, cmd.DeviceType, cmd.DevicePlatform, DeviceConstants.ApprovedByAccount, cmd.DeviceApproval);

        await userRepository.CreateAsync(user, ct);

        var createProfileResult = await CreateProfile(user.Id, cmd.Username);
        if (createProfileResult.Success is false)
            return Result.Fail<SignupDto>(createProfileResult.Error);

        var createClientResult = await chatClientService.CreateClientAsync(user.Id, device.Id, device.ConnectionId, ct);
        if (createClientResult.Success is false)
            return Result.Fail<SignupDto>(SignupFailed);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Ok(new SignupDto(user.Id, device.Id, device.ConnectionId));
    }

    private async Task<SignupChallenge?> ClaimChallenge(Guid challengeId, CancellationToken ct)
    {
        var challenge = await signupChallengeCache.GetChallengeAsync(challengeId, ct);
        if (challenge is null || challenge.Expired)
        {
            logger.LogWarning("Signup challenge {ChallengeId} not found or expired", challengeId);
            return null;
        }

        if (await signupChallengeCache.TryClaimChallengeAsync(challengeId, ct) is false)
        {
            logger.LogWarning("Signup challenge {ChallengeId} was already used", challengeId);
            return null;
        }

        await signupChallengeCache.DeleteChallengeAsync(challengeId, ct);
        return challenge;
    }

    private bool IsProofOfWorkValid(SignupChallenge challenge, SignupCommand cmd)
    {
        var solved = proofOfWorkService.IsSolved(Convert.FromBase64String(challenge.ChallengeValue), cmd.PowNonce, challenge.Difficulty);
        if (!solved)
            logger.LogWarning("Signup challenge {ChallengeId} has an invalid proof of work", challenge.Id);

        return solved;
    }

    private bool AreSignaturesValid(SignupChallenge challenge, SignupCommand cmd)
    {
        var valid = signatureVerifier.VerifyNonce(SignedMessages.SignupChallenge(challenge.ChallengeValue), cmd.ChallengeSignature, cmd.AccountPublicKey)
                    && signatureVerifier.VerifyNonce(SignedMessages.DeviceApproval(cmd.AccountPublicKey, cmd.DevicePublicKey, cmd.SignalIdentityKey), cmd.DeviceApproval, cmd.AccountPublicKey);
        if (!valid)
            logger.LogWarning("Signup challenge {ChallengeId} has an invalid signature", challenge.Id);

        return valid;
    }

    private async Task<bool> AccountExists(SignupCommand cmd, CancellationToken ct)
    {
        var exists = await userRepository.GetSinleAsync(new UserByAccountKeySpec(cmd.AccountPublicKey), ct) is not null;
        if (exists)
            logger.LogWarning("Signup challenge {ChallengeId} used an account key that already exists", cmd.ChallengeId);

        return exists;
    }

    private async ValueTask<Result> CreateProfile(Guid userId, string username)
    {
        var response = await profileClient.GetResponse<CreateUserProfileResponse>(new CreateUserProfileMessage
        {
            CorrelationId = Guid.NewGuid(),
            UserId = userId,
            Username = username
        });

        if (response.Message.IsSuccess)
            return Result.Ok();

        logger.LogError("Failed to create user profile");
        return Result.Fail(Errors.General.UnspecifiedError(response.Message.Error!));
    }
}
