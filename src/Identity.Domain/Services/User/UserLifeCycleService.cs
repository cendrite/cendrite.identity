using Identity.Domain.Events.User;

namespace Identity.Domain.Services.User;

public interface IUserLifeCycleService
{
    void SoftDelete(Aggregates.User user);
    void RevertSoftDelete(Aggregates.User user);
    bool IsUserDeleted(Aggregates.User user);
}

public class UserLifeCycleService : IUserLifeCycleService
{
    public void SoftDelete(Aggregates.User user)
    {
        user.SetDelete();
        user.AddDomainEvent(new UserDeletedEvent(user.Id));
    }

    public void RevertSoftDelete(Aggregates.User user)
    {
        user.RevertDelete();
    }

    public bool IsUserDeleted(Aggregates.User user)
    {
        return user.Deleted.HasValue;
    }
}
