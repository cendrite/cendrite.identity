
using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Test.Unit.Setup.Helpers;

namespace Cendrite.Authentication.Test.Unit.ServiceTest
{
    public class UserLifeCycleServicesTests
    {
        private readonly UserLifeCycleService _userLifeCycleServices = new();

        [Fact]
        public void SoftDelete_Should_Mark_User_As_Deleted()
        {
            // Arrange
            var user = new User(Guid.NewGuid(), "account-key", UserTags.New());

            // Act
            _userLifeCycleServices.SoftDelete(user);

            // Assert
            user.Deleted.Should().NotBeNull();
            user.DomainEvents.Should().ContainSingle(e => e is UserDeletedEvent);
        }

        [Fact]
        public void RevertSoftDelete_Should_Remove_DeletedAt_Date()
        {
            // Arrange
            var user = new User(Guid.NewGuid(), "account-key", UserTags.New());
            _userLifeCycleServices.SoftDelete(user); // Mark as deleted

            // Act
            _userLifeCycleServices.RevertSoftDelete(user);

            // Assert
            user.Deleted.Should().BeNull();
        }

        [Fact]
        public void IsUserDeleted_Should_Return_True_If_User_Is_SoftDeleted()
        {
            // Arrange
            var user = new User(Guid.NewGuid(), "account-key", UserTags.New());
            _userLifeCycleServices.SoftDelete(user); // Mark as deleted

            // Act
            var isDeleted = _userLifeCycleServices.IsUserDeleted(user);

            // Assert
            isDeleted.Should().BeTrue();
        }

        [Fact]
        public void IsUserDeleted_Should_Return_False_If_User_Is_Not_SoftDeleted()
        {
            // Arrange
            var user = new User(Guid.NewGuid(), "account-key", UserTags.New());

            // Act
            var isDeleted = _userLifeCycleServices.IsUserDeleted(user);

            // Assert
            isDeleted.Should().BeFalse();
        }
    }
}
