using Identity.Application.Contracts.RateLimiting;
using StackExchange.Redis;

namespace Identity.Infrastructure.RateLimiting;

/// <summary>
/// Sliding-window rate limiter backed by a Redis/Valkey sorted set, safe across multiple app replicas.
/// Each allowed call is recorded as a member scored by its timestamp; the Lua script atomically
/// evicts entries outside the window, counts what remains, and only admits the new call if under the limit.
/// </summary>
public class RedisSlidingWindowRateLimiter(IConnectionMultiplexer connectionMultiplexer) : IRateLimiter
{
    private const string Script = """
                                   local key = KEYS[1]
                                   local now = tonumber(ARGV[1])
                                   local windowMs = tonumber(ARGV[2])
                                   local limit = tonumber(ARGV[3])
                                   local member = ARGV[4]

                                   redis.call('ZREMRANGEBYSCORE', key, '-inf', now - windowMs)

                                   local count = redis.call('ZCARD', key)
                                   if count >= limit then
                                       return 0
                                   end

                                   redis.call('ZADD', key, now, member)
                                   redis.call('PEXPIRE', key, windowMs)
                                   return 1
                                   """;

    public async Task<bool> IsAllowedAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var db = connectionMultiplexer.GetDatabase();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var member = $"{now}-{Guid.NewGuid()}";

        var result = await db.ScriptEvaluateAsync(
            Script,
            [(RedisKey)key],
            [(RedisValue)now, (RedisValue)(long)window.TotalMilliseconds, (RedisValue)limit, (RedisValue)member]);

        return (long)result == 1;
    }

    private const string ConsumeUpToScript = """
                                              local key = KEYS[1]
                                              local now = tonumber(ARGV[1])
                                              local windowMs = tonumber(ARGV[2])
                                              local limit = tonumber(ARGV[3])
                                              local requested = tonumber(ARGV[4])
                                              local member = ARGV[5]

                                              redis.call('ZREMRANGEBYSCORE', key, '-inf', now - windowMs)

                                              local granted = math.min(requested, limit - redis.call('ZCARD', key))
                                              if granted <= 0 then
                                                  return 0
                                              end

                                              for i = 1, granted do
                                                  redis.call('ZADD', key, now, member .. '-' .. i)
                                              end
                                              redis.call('PEXPIRE', key, windowMs)
                                              return granted
                                              """;

    public async Task<int> ConsumeUpToAsync(string key, int requested, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        if (requested <= 0) return 0;

        var db = connectionMultiplexer.GetDatabase();

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var member = $"{now}-{Guid.NewGuid()}";

        var result = await db.ScriptEvaluateAsync(
            ConsumeUpToScript,
            [(RedisKey)key],
            [(RedisValue)now, (RedisValue)(long)window.TotalMilliseconds, (RedisValue)limit, (RedisValue)requested, (RedisValue)member]);

        return (int)(long)result;
    }

    public async Task ResetAsync(string key, CancellationToken cancellationToken)
    {
        await connectionMultiplexer.GetDatabase().KeyDeleteAsync(key);
    }
}
