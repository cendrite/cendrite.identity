using Identity.Domain.Enums;

namespace Identity.Application.Caching.DeviceLink;

public enum PendingLinkStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public sealed record PendingLink(
    Guid Id,
    Guid UserId,
    Guid IssuingDeviceId,
    string DevicePublicKey,
    string DeviceAppVersion,
    DeviceType DeviceType,
    DevicePlatform DevicePlatform,
    string PollSecretHash,
    DateTime ExpiresAt,
    PendingLinkStatus Status = PendingLinkStatus.Pending,
    Guid? DeviceId = null,
    Guid? DeviceConnectionId = null)
{
    public bool Expired => DateTime.UtcNow > ExpiresAt;
}
