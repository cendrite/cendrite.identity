using FluentAssertions;
using Identity.Application.Features.Device.Commands.ApproveLink;

namespace Identity.Test.Unit.DeviceTest.CommandTest.ApproveLinkTest;

public class ApproveLinkCommandValidatorTest
{
    private readonly ApproveLinkCommandValidator _sut = new();

    private static ApproveLinkCommand ValidCommand() => new()
    {
        UserId = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        PendingLinkId = Guid.NewGuid(),
        Approval = Convert.ToBase64String(new byte[64]),
    };

    [Fact]
    public void Validate_Given_Valid_Command_Should_Pass()
    {
        _sut.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Given_Empty_Pending_Link_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { PendingLinkId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Given_Bad_Signature_Should_Fail()
    {
        _sut.Validate(ValidCommand() with { Approval = "not base64!" }).IsValid.Should().BeFalse();
        _sut.Validate(ValidCommand() with { Approval = Convert.ToBase64String(new byte[32]) }).IsValid.Should().BeFalse();
    }
}
