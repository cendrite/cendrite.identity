using System.Security.Cryptography;

namespace Identity.Test.Unit.Setup.Helpers;

public static class UserTags
{
    public static string New() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6") + RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
