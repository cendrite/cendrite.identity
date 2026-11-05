namespace Identity.Application.Features.User.Commands.Recover;

public sealed record RecoverDto(Guid UserId, Guid DeviceId, Guid DeviceConnectionId);
