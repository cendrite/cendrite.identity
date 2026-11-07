using System.Buffers.Text;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Features.Authentication.Queries.GetTokenIssuerDirectory;
using Identity.Application.Interfaces;
using PrivacyPass.Tokens;

namespace Identity.Test.Unit.AuthenticationTest.QueryTest.GetTokenIssuerDirectoryTest;

public class GetTokenIssuerDirectoryQueryHandlerTest
{
    private readonly ITokenIssuer _fakeTokenIssuer = A.Fake<ITokenIssuer>();
    private readonly GetTokenIssuerDirectoryQueryHandler _sut;

    public GetTokenIssuerDirectoryQueryHandlerTest()
    {
        _sut = new GetTokenIssuerDirectoryQueryHandler(_fakeTokenIssuer);
    }

    [Fact]
    public async Task Handle_Given_Issuer_Key_Should_List_It_As_A_Blind_Rsa_Key()
    {
        // Arrange
        var publicKey = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        A.CallTo(() => _fakeTokenIssuer.GetPublicKeyAsync(A<CancellationToken>._)).Returns(publicKey);

        // Act
        var result = await _sut.Handle(new GetTokenIssuerDirectoryQuery(), CancellationToken.None);

        // Assert
        var key = result.Value!.TokenKeys.Should().ContainSingle().Subject;
        key.TokenType.Should().Be(TokenType.BlindRsa2048);
        key.TokenKey.Should().Be(Base64Url.EncodeToString(publicKey));
    }

    [Fact]
    public async Task Handle_Without_Issuer_Key_Should_List_No_Keys()
    {
        // Arrange
        A.CallTo(() => _fakeTokenIssuer.GetPublicKeyAsync(A<CancellationToken>._)).Returns((byte[]?)null);

        // Act
        var result = await _sut.Handle(new GetTokenIssuerDirectoryQuery(), CancellationToken.None);

        // Assert
        result.Value!.TokenKeys.Should().BeEmpty();
    }
}
