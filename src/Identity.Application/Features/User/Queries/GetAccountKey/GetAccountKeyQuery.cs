using Identity.Application.Abstractions;

namespace Identity.Application.Features.User.Queries.GetAccountKey;

public sealed record GetAccountKeyQuery : IQuery<GetAccountKeyDto>
{
    public required Guid UserId { get; init; }
}
