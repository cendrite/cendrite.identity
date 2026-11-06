using System.Security.Cryptography;
using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;
using Identity.Domain.Events.Device;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Commands.RedeemLinkCode;

public sealed class RedeemLinkCodeCommandHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache,
    IUnitOfWork unitOfWork,
    ILogger<RedeemLinkCodeCommandHandler> logger)
    : ICommandHandler<RedeemLinkCodeCommand, RedeemLinkCodeDto>
{
    private static readonly Error LinkFailed = Errors.General.UnspecifiedError("Linking failed");

    public async ValueTask<Result<RedeemLinkCodeDto>> Handle(RedeemLinkCodeCommand cmd, CancellationToken ct)
    {
        var code = await ClaimCode(Normalize(cmd.Code), ct);
        if (code is null)
            return Result.Fail<RedeemLinkCodeDto>(LinkFailed);

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(code.UserId, excludeDeleted: true), ct);
        var issuingDevice = user?.GetDevices().FirstOrDefault(d => d.Id == code.IssuingDeviceId);
        if (user is null || issuingDevice is null)
        {
            logger.LogWarning("Link code for user {UserId} was redeemed after the issuing device or account went away", code.UserId);
            return Result.Fail<RedeemLinkCodeDto>(LinkFailed);
        }

        var pollSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pending = new PendingLink(
            Guid.NewGuid(),
            user.Id,
            issuingDevice.Id,
            cmd.DevicePublicKey,
            cmd.DeviceAppVersion,
            cmd.DeviceType,
            cmd.DevicePlatform,
            HashSecret(pollSecret),
            code.ExpiresAt);

        await deviceLinkCache.SetPendingAsync(pending, ct);

        user.AddDomainEvent(new DeviceLinkRequestedEvent(user.Id, issuingDevice.Id, issuingDevice.ConnectionId, pending.Id, pending.DevicePublicKey, pending.DevicePlatform));
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Ok(new RedeemLinkCodeDto(pending.Id, pollSecret, pending.ExpiresAt));
    }

    private async Task<LinkCode?> ClaimCode(string code, CancellationToken ct)
    {
        var linkCode = await deviceLinkCache.GetCodeAsync(code, ct);
        if (linkCode is null || linkCode.Expired)
        {
            logger.LogWarning("Unknown or expired link code was redeemed");
            return null;
        }

        if (await deviceLinkCache.TryClaimCodeAsync(code, ct) is false)
        {
            logger.LogWarning("Link code for user {UserId} was already used", linkCode.UserId);
            return null;
        }

        await deviceLinkCache.DeleteCodeAsync(code, ct);
        return linkCode;
    }

    private static string Normalize(string input) =>
        new(input.ToUpperInvariant()
            .Where(c => c is not ('-' or ' '))
            .Select(c => c switch { 'O' => '0', 'I' or 'L' => '1', _ => c })
            .ToArray());

    private static string HashSecret(string secret) => Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(secret)));
}
