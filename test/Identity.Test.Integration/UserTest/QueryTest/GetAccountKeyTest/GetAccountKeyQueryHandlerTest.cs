using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Queries.GetAccountKey;
using Identity.Domain.Aggregates;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using Identity.Test.Integration.Setup.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Test.Integration.UserTest.QueryTest.GetAccountKeyTest;

public class GetAccountKeyQueryHandlerTest : IntegrationTestBase
{
    private readonly GetAccountKeyQueryHandler _sut;

    public GetAccountKeyQueryHandlerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        _sut = new GetAccountKeyQueryHandler(factory.Services.CreateScope().ServiceProvider.GetRequiredService<IUserRepository>());
    }

    [Fact]
    public async Task Handle_Given_Existing_User_Should_Return_The_Stored_Account_Public_Key()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), new KeyPair().PublicKey);
        Db.User.Add(user);
        await Db.SaveChangesAsync();

        // Act
        var result = await _sut.Handle(new GetAccountKeyQuery { UserId = user.Id }, CancellationToken.None);

        // Assert
        result.Value!.AccountPublicKey.Should().Be(user.AccountPublicKey);
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Fail()
    {
        // Act
        var result = await _sut.Handle(new GetAccountKeyQuery { UserId = Guid.NewGuid() }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
    }
}
