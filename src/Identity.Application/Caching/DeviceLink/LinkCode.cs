namespace Identity.Application.Caching.DeviceLink;

public sealed record LinkCode(string Code, Guid UserId, Guid IssuingDeviceId, DateTime ExpiresAt)
{
    public bool Expired => DateTime.UtcNow > ExpiresAt;
}
