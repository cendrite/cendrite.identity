using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;

namespace Identity.Application.Features.User.Queries.GetAccountKey;

public sealed class GetAccountKeyQueryHandler(IUserRepository userRepository) : IQueryHandler<GetAccountKeyQuery, GetAccountKeyDto>
{
    public async ValueTask<Result<GetAccountKeyDto>> Handle(GetAccountKeyQuery q, CancellationToken ct)
    {
        var user = await userRepository.GetSinleAsync(new UserSpec(q.UserId), ct);
        if (user is null)
            return Result.Fail<GetAccountKeyDto>(Errors.General.NotFound(q.UserId));

        return Result.Ok(new GetAccountKeyDto(user.AccountPublicKey));
    }
}
