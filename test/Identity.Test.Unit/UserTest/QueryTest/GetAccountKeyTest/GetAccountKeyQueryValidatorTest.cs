using FluentAssertions;
using Identity.Application.Features.User.Queries.GetAccountKey;

namespace Identity.Test.Unit.UserTest.QueryTest.GetAccountKeyTest;

public class GetAccountKeyQueryValidatorTest
{
    private readonly GetAccountKeyQueryValidator _sut = new();

    [Fact]
    public void Validate_Given_UserId_Should_Pass()
    {
        _sut.Validate(new GetAccountKeyQuery { UserId = Guid.NewGuid() }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Empty_UserId_Should_Fail()
    {
        _sut.Validate(new GetAccountKeyQuery { UserId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
