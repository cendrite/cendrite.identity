using Identity.Application.Abstractions;
using Identity.Domain.Aggregates;

internal sealed class UserByAccountKeySpec(string accountPublicKey)
    : Specification<User>(u => u.AccountPublicKey == accountPublicKey);
