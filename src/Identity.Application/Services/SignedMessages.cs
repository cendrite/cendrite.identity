using System.Text;

namespace Identity.Application.Services;

public static class SignedMessages
{
    private static readonly byte[] AuthContext = Encoding.ASCII.GetBytes("cendrite-auth-v1");
    private static readonly byte[] SignupContext = Encoding.ASCII.GetBytes("cendrite-signup-v1");
    private static readonly byte[] RecoveryContext = Encoding.ASCII.GetBytes("cendrite-recovery-v1");
    private static readonly byte[] DeviceApprovalContext = Encoding.ASCII.GetBytes("device-approval-v1");
    private static readonly byte[] AccountRotationContext = Encoding.ASCII.GetBytes("account-rotate-v1");

    public static string Auth(string nonce) => Build(AuthContext, nonce);

    public static string SignupChallenge(string challenge) => Build(SignupContext, challenge);

    public static string RecoveryChallenge(string challenge) => Build(RecoveryContext, challenge);

    public static string DeviceApproval(string accountPublicKey, string devicePublicKey) =>
        Build(DeviceApprovalContext, accountPublicKey, devicePublicKey);

    public static string AccountRotation(string oldAccountPublicKey, string newAccountPublicKey) =>
        Build(AccountRotationContext, oldAccountPublicKey, newAccountPublicKey);

    private static string Build(byte[] context, params string[] base64Parts) =>
        Convert.ToBase64String([.. context, .. base64Parts.SelectMany(Convert.FromBase64String)]);
}
