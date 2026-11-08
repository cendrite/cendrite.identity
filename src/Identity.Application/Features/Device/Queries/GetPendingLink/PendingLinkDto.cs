namespace Identity.Application.Features.Device.Queries.GetPendingLink;

public sealed record PendingLinkDto(Guid PendingLinkId, string DevicePublicKey, string SignalIdentityKey, string DevicePlatform, string AccountPublicKey, DateTime ExpiresAt);
