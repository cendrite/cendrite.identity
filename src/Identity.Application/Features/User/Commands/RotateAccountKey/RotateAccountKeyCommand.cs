using Identity.Application.Abstractions;

namespace Identity.Application.Features.User.Commands.RotateAccountKey;

public sealed record RotateAccountKeyCommand : ICommand
{
    public required Guid UserId { get; init; }
    public required Guid DeviceId { get; init; }
    public required string NewAccountPublicKey { get; init; }
    public required string Proof { get; init; }
    public required string Approval { get; init; }
}
