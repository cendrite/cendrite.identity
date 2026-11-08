using System.Security.Cryptography;
using Identity.Application.Interfaces;
using Identity.Domain.Constants;

namespace Identity.Application.Services;

public sealed class UserTagGenerator : IUserTagGenerator
{
    public string Generate() =>
        string.Create(UserConstants.TagLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        });
}
