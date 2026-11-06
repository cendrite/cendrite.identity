namespace Identity.Application.Features.Device.Queries.GetLinkStatus;

public sealed record LinkStatusDto(string Status, Guid? UserId, Guid? DeviceId, Guid? DeviceConnectionId);
