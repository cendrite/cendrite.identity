using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Commands.Link;

public sealed record CreateLinkCodeCommand : ICommand<CreateLinkCodeDto>
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
}

public sealed record CreateLinkCodeDto(string Code, DateTime ExpiresAt);
