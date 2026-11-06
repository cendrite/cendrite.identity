using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Queries.GetLinkStatus;

public sealed record GetLinkStatusQuery : IQuery<LinkStatusDto>
{
    public required Guid PendingLinkId { get; init; }
    public required string PollSecret { get; init; }
}
