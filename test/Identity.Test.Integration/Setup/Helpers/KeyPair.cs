using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Identity.Test.Integration.Setup.Helpers;

public sealed class KeyPair
{
    private readonly Ed25519PrivateKeyParameters _private = new(new SecureRandom());

    public string PublicKey => Convert.ToBase64String(_private.GeneratePublicKey().GetEncoded());

    public string Sign(byte[] message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, _private);
        signer.BlockUpdate(message, 0, message.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    public string SignDeviceApproval(string accountPublicKey, string devicePublicKey) =>
        Sign([
            .. Encoding.ASCII.GetBytes("device-approval-v1"),
            .. Convert.FromBase64String(accountPublicKey),
            .. Convert.FromBase64String(devicePublicKey)]);
}
