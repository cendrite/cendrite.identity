using System.Security.Cryptography;
using Cendrite.Domain.Common;
using Identity.Application.Abstractions;
using Identity.Application.Caching.DeviceLink;
using Identity.Application.Contracts.Cache;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Commands.CreateLinkCode;

public sealed class CreateLinkCodeCommandHandler(
    IUserRepository userRepository,
    IDeviceLinkCache deviceLinkCache,
    IRateLimiter rateLimiter)
    : ICommandHandler<CreateLinkCodeCommand, CreateLinkCodeDto>
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 10;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async ValueTask<Result<CreateLinkCodeDto>> Handle(CreateLinkCodeCommand cmd, CancellationToken ct)
    {
        if (await rateLimiter.IsAllowedAsync($"link:code:{cmd.UserId}", 5, TimeSpan.FromHours(1), ct) is false)
            return Result.Fail<CreateLinkCodeDto>(Errors.General.TooManyRequests());

        var user = await userRepository.GetSinleAsync(new UserWithDevicesSpec(cmd.UserId, excludeDeleted: true), ct);
        if (user is null || user.GetDevices().All(d => d.Id != cmd.DeviceId))
            return Result.Fail<CreateLinkCodeDto>(Errors.General.Unauthorized());

        var code = new LinkCode(GenerateCode(), cmd.UserId, cmd.DeviceId, DateTime.UtcNow.Add(Lifetime));
        await deviceLinkCache.AddCodeAsync(code, ct);

        return Result.Ok(new CreateLinkCodeDto(code.Code, code.ExpiresAt));
    }

    private static string GenerateCode() =>
        new(Enumerable.Range(0, CodeLength).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
}
