using Identity.Application.Abstractions;
using Identity.Domain.Aggregates;

internal sealed class UserByConnectionIdWithChainSpec : Specification<User>
{
    public UserByConnectionIdWithChainSpec(Guid connectionId)
        : base(u => u.Deleted == null && u.Devices.Any(d => d.ConnectionId == connectionId))
    {
        AddIncludes($"{nameof(User.Devices)}");
        AddIncludes($"{nameof(User.AccountKeyRotations)}");
    }
}
