using FluentAssertions;
using Identity.Application.Contracts.RateLimiting;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.ServiceTest;

public class RedisSlidingWindowRateLimiterTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : IntegrationTestBase(factory)
{
    private readonly IRateLimiter _rateLimiter = factory.Services.CreateScope().ServiceProvider.GetRequiredService<IRateLimiter>();

    [Fact]
    public async Task ConsumeUpToAsync_Within_The_Limit_Should_Grant_Everything_Requested()
    {
        // Arrange
        var key = $"test:{Guid.NewGuid()}";

        // Act
        var granted = await _rateLimiter.ConsumeUpToAsync(key, 3, 10, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Assert
        granted.Should().Be(3);
    }

    [Fact]
    public async Task ConsumeUpToAsync_Near_The_Limit_Should_Grant_Only_What_Is_Left()
    {
        // Arrange
        var key = $"test:{Guid.NewGuid()}";
        await _rateLimiter.ConsumeUpToAsync(key, 8, 10, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Act
        var granted = await _rateLimiter.ConsumeUpToAsync(key, 5, 10, TimeSpan.FromMinutes(1), CancellationToken.None);
        var afterLimit = await _rateLimiter.ConsumeUpToAsync(key, 1, 10, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Assert
        granted.Should().Be(2);
        afterLimit.Should().Be(0);
    }

    [Fact]
    public async Task ConsumeUpToAsync_Should_Share_The_Limit_With_Single_Calls()
    {
        // Arrange
        var key = $"test:{Guid.NewGuid()}";
        await _rateLimiter.ConsumeUpToAsync(key, 2, 3, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Act
        var allowed = await _rateLimiter.IsAllowedAsync(key, 3, TimeSpan.FromMinutes(1), CancellationToken.None);
        var refused = await _rateLimiter.IsAllowedAsync(key, 3, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Assert
        allowed.Should().BeTrue();
        refused.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeUpToAsync_Should_Free_Slots_Once_They_Leave_The_Window()
    {
        // Arrange
        var key = $"test:{Guid.NewGuid()}";
        await _rateLimiter.ConsumeUpToAsync(key, 2, 2, TimeSpan.FromMilliseconds(200), CancellationToken.None);
        await Task.Delay(300);

        // Act
        var granted = await _rateLimiter.ConsumeUpToAsync(key, 2, 2, TimeSpan.FromMilliseconds(200), CancellationToken.None);

        // Assert
        granted.Should().Be(2);
    }
}
