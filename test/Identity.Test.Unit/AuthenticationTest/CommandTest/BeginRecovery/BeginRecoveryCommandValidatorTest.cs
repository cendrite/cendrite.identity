using FluentAssertions;
using Identity.Application.Features.Authentication.Commands.BeginRecovery;

namespace Identity.Test.Unit.AuthenticationTest.CommandTest.BeginRecovery;

public class BeginRecoveryCommandValidatorTest
{
    private readonly BeginRecoveryCommandValidator _sut = new();

    [Fact]
    public void Validate_Given_32_Byte_Key_Should_Pass()
    {
        _sut.Validate(new BeginRecoveryCommand { AccountPublicKey = Convert.ToBase64String(new byte[32]) }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void Validate_Given_Invalid_Key_Should_Fail(string key)
    {
        _sut.Validate(new BeginRecoveryCommand { AccountPublicKey = key }).IsValid.Should().BeFalse();
    }
}
