using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Aggregates;
using Identity.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public class UserRepository(IdentityDbContext context) : IUserRepository
{
    public async Task CreateAsync(User entity, CancellationToken ct = default)
    {
        await context.User.AddAsync(entity, ct);
    }

    public async Task<IReadOnlyCollection<User>> GetUsersAsync(Guid[] userIds, CancellationToken ct = default)
    {
        List<Guid> ids = [.. userIds];
        return await context.User.Include(u => u.Devices).Where(u => ids.Contains(u.Id)).ToListAsync(ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.User.FindAsync([id], ct);
    }

    public Task UpdateAsync(User entity, CancellationToken ct = default)
    {
        context.User.Update(entity);
        return Task.CompletedTask;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.User.FirstOrDefaultAsync(c => c.Email!.Address.Equals(email), ct);
    }

    public Task<User?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct = default)
    {
        return context.User.FirstOrDefaultAsync(c => c.Devices.Any(c => c.Id == deviceId), ct);
    }

    public async Task<User?> GetSinleAsync(ISpecification<User> spec, CancellationToken ct = default)
    {
        var q = context.User.Where(spec.Criteria);

        q = spec.Includes.Aggregate(q, (current, include) => current.Include(include));

        return await q.FirstOrDefaultAsync(ct);
    }

    public Task<List<User>> GetListAsync(ISpecification<User> spec, CancellationToken ct = default)
    {
        var q = context.User.Where(spec.Criteria);

        q = spec.Includes.Aggregate(q, (current, include) => current.Include(include));

        return q.ToListAsync(ct);
    }

    public void Delete(User entity)
    {
        throw new NotImplementedException();
    }
}
