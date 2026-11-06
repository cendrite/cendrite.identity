using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Commands.Rename;

public sealed record RenameDeviceCommand : ICommand
{
    public required Guid UserId { get; init; }
    public required Guid TargetDeviceId { get; init; }
    public required string Name { get; init; }
}
