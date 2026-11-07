using Identity.Domain.Aggregates;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Context;

public class IdentityDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> User { get; init; }
    public DbSet<Device> Device { get; init; }
    public DbSet<AccountKeyRotation> AccountKeyRotation { get; init; }
    public DbSet<OutboxMessage> OutboxMessage { get; init; }
    public DbSet<SigningKey> SigningKey { get; init; }
    public DbSet<TokenIssuerKey> TokenIssuerKey { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
