using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Application.Features.Authentication.Commands.Login;
using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;

namespace Identity.Test.Integration.UserTest.EndpointTest;

public class LoginEndpointTest(IntegrationTestFactory<Program, IdentityDbContext> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Given_Valid_Login_Request_Should_Return_200_And_Token()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), true);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = "TestPassword?123",
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/authentication/login", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Db.VerificationCode.Should().HaveCount(1);

    }

    [Fact]
    public async Task Given_Wrong_Password_Should_Return_401()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Email.Create("test@mail.dk"), Password.Create("TestPassword?123"), true);

        Db.User.Add(user);
        await Db.SaveChangesAsync();

        var command = new LoginCommand
        {
            Email = user.Email!.Address,
            Password = "TestPassword?12323123", // Invalid password
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/authentication/login", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}