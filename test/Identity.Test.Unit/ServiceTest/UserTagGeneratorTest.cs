using FluentAssertions;
using Identity.Application.Services;

namespace Identity.Test.Unit.ServiceTest;

public class UserTagGeneratorTest
{
    [Fact]
    public void Generate_Should_Return_Twelve_Digits()
    {
        var tags = Enumerable.Range(0, 50).Select(_ => new UserTagGenerator().Generate()).ToList();

        tags.Should().AllSatisfy(tag => tag.Should().MatchRegex("^[0-9]{12}$"));
        tags.Distinct().Should().HaveCountGreaterThan(45);
    }
}
