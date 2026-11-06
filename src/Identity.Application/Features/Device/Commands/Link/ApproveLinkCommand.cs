using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Commands.Link;

public sealed record ApproveLinkCommand : ICommand
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid PendingLinkId { get; init; }
    public required string Approval { get; init; }
}

public sealed record RejectLinkCommand : ICommand
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid PendingLinkId { get; init; }
}
