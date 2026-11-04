using System.Net;
using System.Net.Http.Json;
using Cendrite.Message.Contracts.Messages.Profile;
using FluentAssertions;
using Identity.Application.Features.User.Commands.Create;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;

namespace Identity.Test.Integration.UserTest.EndpointTest;

public class CreateUserEndpointTest(IntegrationTestFactory<Program, IdentityDbContext> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Valid_Request_Should_Create_User_And_Return_200_Ok()
    {
        // Arrange
        var req = new CreateUserCommand
        {
            Username = "TestUser",
            Email = "test@email.dk",
            Password = "TestPassword3?"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/user", req);

        // Assert
        Harness.Published.Select<CreateUserProfileMessage>().Where(cr => cr.Context.Message.Username == "TestUser").Should().HaveCount(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Db.User.Count().Should().Be(1);
        Db.User.First().Email!.Address.Should().Be(req.Email);
        Db.OutboxMessage.Count().Should().Be(1);
    }

    [Fact]
    public async Task Invalid_Request_Should_Return_400_BadRequest()
    {
        // Arrange
        var req = new CreateUserCommand
        {
            Username = "TestUser",
            Email = "testemail.dk",
            Password = "TestPassword3?"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/user", req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Db.User.Count().Should().Be(0);
        Db.OutboxMessage.Count().Should().Be(0);
    }
}