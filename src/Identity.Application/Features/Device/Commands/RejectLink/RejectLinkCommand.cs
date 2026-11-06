using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Commands.RejectLink;

public sealed record RejectLinkCommand : ICommand
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required Guid PendingLinkId { get; init; }
}
