using Identity.Domain.Events.User;

namespace Identity.Domain.Services.User;

public interface IAuthenticationService
{
    void Logout(Aggregates.User user, Guid deviceId);
}

public class AuthenticationService : IAuthenticationService
{
    public void Logout(Aggregates.User user, Guid deviceId)
    {
        user.AddDomainEvent(new UserLoggedOutEvent(user.Id, deviceId));
        var device = user.GetDevice(deviceId);

        device.SetLastSeen();
        device.SetDelete();
    }
}
