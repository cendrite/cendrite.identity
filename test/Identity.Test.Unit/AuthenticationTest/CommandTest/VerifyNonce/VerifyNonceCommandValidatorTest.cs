using FluentAssertions;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;

namespace Identity.Test.Unit.AuthenticationTest.CommandTest.VerifyNonce;

public class VerifyNonceCommandValidatorTest
{
    private readonly VerifyNonceCommandValidator _validator = new();

    [Fact]
    public void VerifyNonceCommandValidator_WhenUserIdIsEmpty_ShouldHaveError()
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.Empty,
            NonceId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Nonce = "nonce"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(VerifyNonceCommand.UserId));
    }

    [Fact]
    public void VerifyNonceCommandValidator_WhenNonceIdIsEmpty_ShouldHaveError()
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.NewGuid(),
            NonceId = Guid.Empty,
            DeviceId = Guid.NewGuid(),
            Nonce = "nonce"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(VerifyNonceCommand.NonceId));
    }

    [Fact]
    public void VerifyNonceCommandValidator_WhenDeviceIdIsEmpty_ShouldHaveError()
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            DeviceId = Guid.Empty,
            Nonce = "nonce"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(VerifyNonceCommand.DeviceId));
    }

    [Fact]
    public void VerifyNonceCommandValidator_WhenNonceIsEmpty_ShouldHaveError()
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Nonce = string.Empty
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(VerifyNonceCommand.Nonce));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void VerifyNonceCommandValidator_WhenTokenRequestsAreWithinTheLimit_ShouldBeValid(int count)
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Nonce = "nonce",
            TokenRequests = [.. Enumerable.Repeat("request", count)],
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyNonceCommandValidator_WhenTooManyTokenRequests_ShouldHaveError()
    {
        var command = new VerifyNonceCommand
        {
            UserId = Guid.NewGuid(),
            NonceId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Nonce = "nonce",
            TokenRequests = ["1", "2", "3", "4", "5", "6"],
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(VerifyNonceCommand.TokenRequests));
    }
}
