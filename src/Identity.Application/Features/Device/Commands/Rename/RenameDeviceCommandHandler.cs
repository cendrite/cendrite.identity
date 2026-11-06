using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Features.Device.Commands.Rename;

public sealed class RenameDeviceCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<RenameDeviceCommandHandler> logger)
    : ICommandHandler<RenameDeviceCommand>
{
    public async ValueTask<Result> Handle(RenameDeviceCommand cmd, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(cmd.UserId), ct);
        var device = user?.GetDevices().FirstOrDefault(d => d.Id == cmd.TargetDeviceId);
        if (device is null)
        {
            logger.LogWarning("Device {DeviceId} not found for user {UserId}", cmd.TargetDeviceId, cmd.UserId);
            return Result.Fail(Errors.General.NotFound(cmd.TargetDeviceId));
        }

        device.Rename(cmd.Name);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
