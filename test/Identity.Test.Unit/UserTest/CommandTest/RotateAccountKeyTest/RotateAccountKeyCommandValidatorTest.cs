using FluentAssertions;
using Identity.Application.Features.User.Commands.RotateAccountKey;

namespace Identity.Test.Unit.UserTest.CommandTest.RotateAccountKeyTest;

public class RotateAccountKeyCommandValidatorTest
{
    private readonly RotateAccountKeyCommandValidator _sut = new();

    private static string Base64(int length) => Convert.ToBase64String(new byte[length]);

    private static RotateAccountKeyCommand ValidCommand() => new()
    {
        UserId = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        NewAccountPublicKey = Base64(32),
        Proof = Base64(64),
        Approval = Base64(64),
    };

    [Fact]
    public void Validate_Given_Valid_Command_Should_Pass()
    {
        _sut.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Bad_Key_Or_Signatures_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { NewAccountPublicKey = "not base64!" }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { NewAccountPublicKey = Base64(33) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { Proof = Base64(32) }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { Approval = Base64(65) }).IsValid.Should().BeFalse();
    }
}
