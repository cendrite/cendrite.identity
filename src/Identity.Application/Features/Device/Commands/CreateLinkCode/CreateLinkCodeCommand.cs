using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Commands.CreateLinkCode;

public sealed record CreateLinkCodeCommand : ICommand<CreateLinkCodeDto>
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
}
