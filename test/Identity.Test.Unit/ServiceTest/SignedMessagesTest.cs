using System.Text;
using FluentAssertions;
using Identity.Application.Services;

namespace Identity.Test.Unit.ServiceTest;

public class SignedMessagesTest
{
    private static readonly byte[] KeyA = Enumerable.Repeat((byte)1, 32).ToArray();
    private static readonly byte[] KeyB = Enumerable.Repeat((byte)2, 32).ToArray();

    private static string Expected(string label, params byte[][] parts) =>
        Convert.ToBase64String([.. Encoding.ASCII.GetBytes(label), .. parts.SelectMany(p => p)]);

    [Fact]
    public void Each_Message_Should_Use_Its_Own_Label_Followed_By_The_Raw_Bytes()
    {
        var a = Convert.ToBase64String(KeyA);
        var b = Convert.ToBase64String(KeyB);

        SignedMessages.Auth(a).Should().Be(Expected("cendrite-auth-v1", KeyA));
        SignedMessages.SignupChallenge(a).Should().Be(Expected("cendrite-signup-v1", KeyA));
        SignedMessages.RecoveryChallenge(a).Should().Be(Expected("cendrite-recovery-v1", KeyA));
        SignedMessages.DeviceApproval(a, b, a).Should().Be(Expected("device-approval-v1", KeyA, KeyB, KeyA));
        SignedMessages.AccountRotation(a, b).Should().Be(Expected("account-rotate-v1", KeyA, KeyB));
    }
}
