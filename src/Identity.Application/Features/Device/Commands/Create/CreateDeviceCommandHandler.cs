using Cendrite.Domain.Common;
using Identity.Application.Caching.LoginNonce;
using Identity.Domain.Common;
using Identity.Domain.Services.User;
using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Responses.Client;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.Repository;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Commands.Create;

public class CreateDeviceCommandHandler(
    IUserRepository userRepository,
    IRequestClient<CreateClientMessage> requestClient,
    ILoginNonceCache loginNonceCache,
    IDeviceService deviceService,
    IUnitOfWork unitOfWork,
    ILogger<CreateDeviceCommandHandler> logger)
    : ICommandHandler<CreateDeviceCommand, CreateDeviceDto>
{
    public async ValueTask<Result<CreateDeviceDto>> Handle(CreateDeviceCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(cmd.UserId, excludeDeleted: true), ct);
        if (user is null)
        {
            logger.LogWarning("User {UserId} not found or deleted", cmd.UserId);
            return Result.Fail<CreateDeviceDto>(Errors.General.NotFound(cmd.UserId));
        }

        var loginNonce = await loginNonceCache.GetNonceAsync(cmd.LoginNonceId, ct);

        if (loginNonce is null || !loginNonce.IsValidFor(cmd.UserId, cmd.LoginNonce, LoginNonceStage.CodeVerified))
        {
            logger.LogWarning("Login nonce {LoginNonceId} not found or invalid for user with ID: {ID}", cmd.LoginNonceId, cmd.UserId);
            return Result.Fail<CreateDeviceDto>(Errors.General.Unauthorized());
        }

        if (await loginNonceCache.TryClaimNonceAsync(cmd.LoginNonceId, ct) is false)
        {
            logger.LogWarning("Login nonce {LoginNonceId} for user with ID: {ID} is already being used", cmd.LoginNonceId, cmd.UserId);
            return Result.Fail<CreateDeviceDto>(Errors.General.Unauthorized());
        }

        var device = deviceService.RegisterDevice(user, cmd.Base64DevicePublicKey, cmd.DeviceAppVersion, cmd.DeviceType, cmd.DevicePlatform, null, null);

        var createClientResult = await CreateClient(device.Id, device.ConnectionId, device.UserId);

        if (createClientResult.Success is false)
        {
            // Nothing was saved, so let the client retry with the same nonce.
            await loginNonceCache.ReleaseNonceClaimAsync(cmd.LoginNonceId, ct);
            return Result.Fail<CreateDeviceDto>(createClientResult.Error);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var dto = CreateDeviceDto.Map(device);

        await loginNonceCache.DeteleNonceAsync(cmd.LoginNonceId, ct);

        return Result.Ok(dto);
    }

    private async ValueTask<Result> CreateClient(Guid deviceId, Guid connectionId, Guid userId)
    {
        var response = await requestClient.GetResponse<CreateClientResponse>(new CreateClientMessage
        {
            CorrelationId = Guid.NewGuid(),
            DeviceId = deviceId,
            ConnectionId = connectionId,
            UserId = userId
        });

        if (response.Message.IsSuccess)
            return Result.Ok();

        logger.LogError("Got a fail response from the Chat server attempting to create a Clients for Device with ID {DeviceId}", deviceId);
        return Result.Fail(Errors.General.UnspecifiedError("Failed to create profile"));
    }
}
