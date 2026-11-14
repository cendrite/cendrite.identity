using System.Security.Cryptography;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.RateLimiting;
using Identity.Application.Contracts.Repository;
using Identity.Application.Services;
using Identity.Domain.Aggregates;
using Identity.Test.Unit.Setup.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using PrivacyPass.Keys;

namespace Identity.Test.Unit.AuthenticationTest.ServiceTest;

public class TokenIssuerTest
{
    private readonly ITokenIssuerKeyRepository _fakeKeys = A.Fake<ITokenIssuerKeyRepository>();
    private readonly IRateLimiter _fakeRateLimiter = A.Fake<IRateLimiter>();
    private readonly TokenIssuer _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public TokenIssuerTest()
    {
        A.CallTo(() => _fakeKeys.GetCurrentAsync(A<CancellationToken>._)).Returns(new TokenIssuerKey(
            Guid.NewGuid(),
            PrivacyPassFixture.Values.IssuerPublicKey(),
            PrivacyPassFixture.Values.IssuerPrivateKeyPkcs8()));

        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._))
            .ReturnsLazily((string _, int requested, int _, TimeSpan _, CancellationToken _) => requested);

        _sut = new TokenIssuer(_fakeKeys, _fakeRateLimiter, NullLogger<TokenIssuer>.Instance);
    }

    [Fact]
    public async Task IssueAsync_Given_Client_Token_Request_Should_Return_The_Client_Issuer_Response()
    {
        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        responses.Should().Equal(PrivacyPassFixture.Values.TokenResponse);
    }

    [Fact]
    public async Task IssueAsync_Should_Count_Every_Token_Against_The_User_Budget()
    {
        // Act
        await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest, PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync($"tokens:issue:{UserId}", 2, 30, TimeSpan.FromHours(24), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task IssueAsync_Near_The_Budget_Should_Issue_Only_What_Fits()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(1);

        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest, PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        responses.Should().Equal(PrivacyPassFixture.Values.TokenResponse);
    }

    [Fact]
    public async Task IssueAsync_Without_Token_Requests_Should_Return_None_And_Not_Count()
    {
        // Act
        var responses = await _sut.IssueAsync(UserId, []);

        // Assert
        responses.Should().BeEmpty();
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task IssueAsync_Over_The_Budget_Should_Return_None()
    {
        // Arrange
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).Returns(0);

        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task IssueAsync_Without_Issuer_Key_Should_Return_None_And_Not_Count()
    {
        // Arrange
        A.CallTo(() => _fakeKeys.GetCurrentAsync(A<CancellationToken>._)).Returns((TokenIssuerKey?)null);

        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        responses.Should().BeEmpty();
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("not base64 ***")]
    [InlineData("AAAA")]
    public async Task IssueAsync_Given_Malformed_Token_Request_Should_Return_None_And_Not_Count(string tokenRequest)
    {
        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest, tokenRequest]);

        // Assert
        responses.Should().BeEmpty();
        A.CallTo(() => _fakeRateLimiter.ConsumeUpToAsync(A<string>._, A<int>._, A<int>._, A<TimeSpan>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task IssueAsync_Given_Token_Request_For_Other_Key_Should_Return_None()
    {
        // Arrange
        using var otherKey = RSA.Create(2048);
        A.CallTo(() => _fakeKeys.GetCurrentAsync(A<CancellationToken>._)).Returns(new TokenIssuerKey(
            Guid.NewGuid(), TokenKeyId.EncodePublicKey(otherKey), otherKey.ExportPkcs8PrivateKey()));

        // Act
        var responses = await _sut.IssueAsync(UserId, [PrivacyPassFixture.Values.TokenRequest]);

        // Assert
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPublicKeyAsync_Should_Return_The_Current_Public_Key()
    {
        // Act
        var publicKey = await _sut.GetPublicKeyAsync();

        // Assert
        publicKey.Should().Equal(PrivacyPassFixture.Values.IssuerPublicKey());
    }
}
