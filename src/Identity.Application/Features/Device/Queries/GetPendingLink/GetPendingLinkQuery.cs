using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Queries.GetPendingLink;

public sealed record GetPendingLinkQuery : IQuery<PendingLinkDto>
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid PendingLinkId { get; init; }
}
