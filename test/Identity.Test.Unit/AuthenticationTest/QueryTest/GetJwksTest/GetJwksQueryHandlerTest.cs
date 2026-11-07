using FakeItEasy;
using FluentAssertions;
using Identity.Application.Dtos;
using Identity.Application.Features.Authentication.Queries.GetJwks;
using Identity.Application.Interfaces;

namespace Identity.Test.Unit.AuthenticationTest.QueryTest.GetJwksTest;

public class GetJwksQueryHandlerTest
{
    [Fact]
    public async Task Handle_Should_Return_The_Keys_From_The_Jwt_Service()
    {
        // Arrange
        var keys = new List<JwksDto> { new() { Kty = "RSA", Use = "sig", Alg = "RS256", Kid = "kid-1", N = "n", E = "AQAB" } };
        var jwtService = A.Fake<IJwtService>();
        A.CallTo(() => jwtService.GenerateJwks(A<CancellationToken>._)).Returns(keys);

        // Act
        var result = await new GetJwksQueryHandler(jwtService).Handle(new GetJwksQuery(), CancellationToken.None);

        // Assert
        result.Value!.Keys.Should().BeEquivalentTo(keys);
    }
}
