using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Queries.GetAccountKey;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;

namespace Identity.Test.Unit.UserTest.QueryTest.GetAccountKeyTest;

public class GetAccountKeyQueryHandlerTest
{
    private readonly IUserRepository _fakeRepo = A.Fake<IUserRepository>();
    private readonly GetAccountKeyQueryHandler _sut;

    public GetAccountKeyQueryHandlerTest()
    {
        _sut = new GetAccountKeyQueryHandler(_fakeRepo);
    }

    [Fact]
    public async Task Handle_Given_Existing_User_Should_Return_The_Account_Public_Key()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), "account-key");
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns(user);

        // Act
        var result = await _sut.Handle(new GetAccountKeyQuery { UserId = user.Id }, CancellationToken.None);

        // Assert
        result.Value.Should().BeEquivalentTo(new GetAccountKeyDto("account-key"));
    }

    [Fact]
    public async Task Handle_Given_Unknown_User_Should_Return_NotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new GetAccountKeyQuery { UserId = userId }, CancellationToken.None);

        // Assert
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(userId));
    }
}
