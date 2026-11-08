using Identity.Domain.Enums;

namespace Identity.Application.Caching.DeviceLink;

public sealed record PendingLink(
    Guid Id,
    Guid UserId,
    Guid IssuingDeviceId,
    string DevicePublicKey,
    string SignalIdentityKey,
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

    public bool IsOpenFor(Guid userId, Guid deviceId) =>
        !Expired && Status == PendingLinkStatus.Pending && UserId == userId && IssuingDeviceId == deviceId;
}
