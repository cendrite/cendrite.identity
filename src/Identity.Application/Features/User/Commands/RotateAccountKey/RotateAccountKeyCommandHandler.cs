using System.Text;
using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Domain.Common;
using Identity.Domain.Events.User;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.User.Commands.RotateAccountKey;

public sealed class RotateAccountKeyCommandHandler(
    IUserRepository userRepository,
    IVerifyNonceService signatureVerifier,
    IRateLimiter rateLimiter,
    IUnitOfWork unitOfWork,
    ILogger<RotateAccountKeyCommandHandler> logger)
    : ICommandHandler<RotateAccountKeyCommand>
{
    private static readonly byte[] RotationContext = Encoding.ASCII.GetBytes("account-rotate-v1");
    private static readonly Error RotationFailed = Errors.General.UnspecifiedError("Rotation failed");

    public async ValueTask<Result> Handle(RotateAccountKeyCommand cmd, CancellationToken ct)
    {
        if (await rateLimiter.IsAllowedAsync($"account-key:rotate:{cmd.UserId}", 3, TimeSpan.FromDays(1), ct) is false)
            return Result.Fail(Errors.General.TooManyRequests());

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(cmd.UserId, excludeDeleted: true), ct);

        var device = user?.GetDevices().FirstOrDefault(d => d.Id == cmd.DeviceId);

        if (user is null || device is null)
            return Result.Fail(Errors.General.Unauthorized());

        if (!AreSignaturesValid(user.AccountPublicKey, cmd, device.PublicKey))
        {
            logger.LogWarning("Device {DeviceId} sent an invalid account key rotation", device.Id);
            return Result.Fail(RotationFailed);
        }

        if (await KeyInUse(cmd.NewAccountPublicKey, ct))
        {
            logger.LogWarning("Device {DeviceId} tried to rotate to an account key that is already in use", device.Id);
            return Result.Fail(RotationFailed);
        }

        user.RotateAccountKey(device.Id, cmd.NewAccountPublicKey, cmd.Proof, cmd.Approval);
        user.AddDomainEvent(new AccountKeyRotatedEvent(user.Id, device.Id, device.ConnectionId, device.Name));

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Ok();
    }

    private bool AreSignaturesValid(string oldAccountPublicKey, RotateAccountKeyCommand cmd, string devicePublicKey)
    {
        byte[] message = [.. RotationContext, .. Convert.FromBase64String(oldAccountPublicKey), .. Convert.FromBase64String(cmd.NewAccountPublicKey)];
        var encoded = Convert.ToBase64String(message);

        return signatureVerifier.VerifyNonce(encoded, cmd.Proof, cmd.NewAccountPublicKey)
               && signatureVerifier.VerifyNonce(encoded, cmd.Approval, devicePublicKey);
    }

    private async Task<bool> KeyInUse(string accountPublicKey, CancellationToken ct) =>
        await userRepository.GetSinleAsync(new UserByAccountKeySpec(accountPublicKey), ct) is not null;
}
