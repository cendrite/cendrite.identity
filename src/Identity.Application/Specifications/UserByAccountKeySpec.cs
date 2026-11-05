using Identity.Application.Abstractions;
using Identity.Domain.Aggregates;

internal sealed class UserByAccountKeySpec(string accountPublicKey, bool excludeDeleted = false)
    : Specification<User>(u => u.AccountPublicKey == accountPublicKey && (!excludeDeleted || u.Deleted == null));
