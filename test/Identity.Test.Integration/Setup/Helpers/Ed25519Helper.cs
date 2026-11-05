using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Identity.Test.Integration.Setup.Helpers;

public static class Ed25519Helper
{
    public static string SignNonce(string nonce, string privateKeyBase64)
    {
        byte[] nonceBytes = [.. Encoding.ASCII.GetBytes("cendrite-auth-v1"), .. Convert.FromBase64String(nonce)];
        var privateKey = Convert.FromBase64String(privateKeyBase64);
        var privateKeyParam = new Ed25519PrivateKeyParameters(privateKey, 0);

        var signer = SignerUtilities.GetSigner("Ed25519");
        signer.Init(true, privateKeyParam);
        signer.BlockUpdate(nonceBytes, 0, nonceBytes.Length);
        var signature = signer.GenerateSignature();
        return Convert.ToBase64String(signature);
    }
}