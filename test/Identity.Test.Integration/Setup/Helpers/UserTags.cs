using System.Security.Cryptography;

namespace Identity.Test.Integration.Setup.Helpers;

public static class UserTags
{
    public static string New() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6") + RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
