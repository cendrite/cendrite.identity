using Identity.Application.Abstractions;
using Identity.Domain.Aggregates;

internal sealed class UserByTagSpec(string userTag) : Specification<User>(u => u.UserTag == userTag);
