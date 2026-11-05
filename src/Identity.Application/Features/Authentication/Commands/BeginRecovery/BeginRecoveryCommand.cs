using Identity.Application.Abstractions;

namespace Identity.Application.Features.Authentication.Commands.BeginRecovery;

public sealed record BeginRecoveryCommand : ICommand<BeginRecoveryDto>
{
    public required string AccountPublicKey { get; init; }
}
