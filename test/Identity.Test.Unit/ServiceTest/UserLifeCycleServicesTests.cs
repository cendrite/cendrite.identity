
using FluentAssertions;
using Identity.Domain.Aggregates;
using Identity.Domain.Events.User;
using Identity.Domain.Services.User;
using Identity.Domain.ValueObjects;

namespace Cendrite.Authentication.Test.Unit.ServiceTest
{
    public class UserLifeCycleServicesTests
    {
        private readonly UserLifeCycleService _userLifeCycleServices = new();

        [Fact]
        public void CreateUser_Should_Fail_When_Email_Is_Invalid()
        {
            // Arrange
            const string invalidEmail = "invalidemail";
            const string password = "Password123!";

            // Act
            var result = _userLifeCycleServices.CreateUser(invalidEmail, password);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Description.Should().Be("Invalid email address."); // Adjust based on actual message in Email.Create
        }

        [Fact]
        public void CreateUser_Should_Fail_When_Password_Is_Invalid()
        {
            // Arrange
            const string email = "test@mail.com";
            const string invalidPassword = "short"; // Assuming password should meet certain criteria

            // Act
            var result = _userLifeCycleServices.CreateUser(email, invalidPassword);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Description.Should().Contain("Incorrect password:");
        }

        [Fact]
        public void CreateUser_Should_Succeed_When_Valid_Email_And_Password_Are_Provided()
        {
            // Arrange
            const string email = "test@mail.com";
            const string password = "Password123!";

            // Act
            var result = _userLifeCycleServices.CreateUser(email, password);

            // Assert
            result.Success.Should().BeTrue();
            result.Value.Email!.Address.Should().Be(email);
            result.Value.IsVerified.Should().BeFalse();
            result.Value.VerificationCodes.Should().HaveCount(1);
        }

        [Fact]
        public void CreateUser_Should_Add_UserCreatedEvent()
        {
            // Arrange
            const string email = "test@mail.com";
            const string password = "Password123!";

            // Act
            var result = _userLifeCycleServices.CreateUser(email, password);

            // Assert
            result.Success.Should().BeTrue();
            var user = result.Value;

            user.DomainEvents.Should().ContainSingle(e => e is UserCreatedEvent);

            var userCreatedEvent = user.DomainEvents.OfType<UserCreatedEvent>().FirstOrDefault();
            userCreatedEvent.Should().NotBeNull();
            userCreatedEvent!.UserId.Should().Be(user.Id);
        }


        [Fact]
        public void SoftDelete_Should_Mark_User_As_Deleted()
        {
            // Arrange
            var email = Email.Create("test@mail.com").Value!;
            var password = Password.Create("Password123!").Value!;
            var user = new User(Guid.NewGuid(), email, password, false);

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
            var email = Email.Create("test@mail.com").Value!;
            var password = Password.Create("Password123!").Value!;
            var user = new User(Guid.NewGuid(), email, password, false);
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
            var email = Email.Create("test@mail.com").Value!;
            var password = Password.Create("Password123!").Value!;
            var user = new User(Guid.NewGuid(), email, password, false);
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
            var email = Email.Create("test@mail.com").Value!;
            var password = Password.Create("Password123!").Value!;
            var user = new User(Guid.NewGuid(), email, password, false);

            // Act
            var isDeleted = _userLifeCycleServices.IsUserDeleted(user);

            // Assert
            isDeleted.Should().BeFalse();
        }
    }
}
