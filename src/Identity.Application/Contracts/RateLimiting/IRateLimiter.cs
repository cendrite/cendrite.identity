namespace Identity.Application.Contracts.RateLimiting;

public interface IRateLimiter
{
    /// <summary>
    /// Checks and consumes one slot of a sliding-window rate limit for the given key.
    /// </summary>
    /// <param name="key">Unique identifier for the thing being limited, e.g. "login:issue:{userId}".</param>
    /// <param name="limit">Maximum number of allowed calls within the window.</param>
    /// <param name="window">Size of the sliding window.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if the call is allowed (and has been counted), false if the limit has been exceeded.</returns>
    Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken);

    Task<int> ConsumeUpToAsync(string key, int requested, int limit, TimeSpan window, CancellationToken cancellationToken);

    Task ResetAsync(string key, CancellationToken cancellationToken);
}
