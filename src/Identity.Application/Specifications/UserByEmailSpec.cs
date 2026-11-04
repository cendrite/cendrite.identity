using Identity.Application.Abstractions;
using Identity.Domain.Aggregates;

internal sealed class UserByEmailSpec(string email, bool excludeDeleted = false)
    : Specification<User>(u => u.Email!.Address == email && (!excludeDeleted || u.Deleted == null));
