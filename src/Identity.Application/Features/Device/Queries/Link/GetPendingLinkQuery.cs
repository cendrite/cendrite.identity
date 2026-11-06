using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Queries.Link;

public sealed record GetPendingLinkQuery : IQuery<PendingLinkDto>
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid PendingLinkId { get; init; }
}

public sealed record PendingLinkDto(Guid PendingLinkId, string DevicePublicKey, string DevicePlatform, string AccountPublicKey, DateTime ExpiresAt);

public sealed record GetLinkStatusQuery : IQuery<LinkStatusDto>
{
    public required Guid PendingLinkId { get; init; }
    public required string PollSecret { get; init; }
}

public sealed record LinkStatusDto(string Status, Guid? UserId, Guid? DeviceId, Guid? DeviceConnectionId);
