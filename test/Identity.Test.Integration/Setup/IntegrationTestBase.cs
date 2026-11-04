using AutoFixture;
using Identity.Infrastructure.Caching;
using Identity.Infrastructure.Persistence.Context;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.Setup;

[Collection("AuthenticationApplication")]
public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IdentityDbContext Db;
    protected readonly HttpClient Client;
    protected readonly ITestHarness Harness;
    protected readonly IValkeyCacheService Cache;
    protected readonly Fixture Fixture = new Fixture();

    public IntegrationTestBase(IntegrationTestFactory<Program, IdentityDbContext> factory)
    {
        Harness = factory.Services.GetTestHarness();
        var scope = factory.Services.CreateScope();
        Db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Cache = scope.ServiceProvider.GetRequiredService<IValkeyCacheService>();
        Db.Database.EnsureCreated();
        Client = factory.CreateClient();
    }

    public Task InitializeAsync() => Harness.Start();

    public async Task DisposeAsync()
    {
        await Db.VerificationCode.ExecuteDeleteAsync();
        await Db.User.ExecuteDeleteAsync();
        await Db.OutboxMessage.ExecuteDeleteAsync();
        await Db.Device.ExecuteDeleteAsync();
        await Harness.Stop();
    }
}