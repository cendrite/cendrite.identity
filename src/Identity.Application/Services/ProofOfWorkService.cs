using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using Identity.Application.Interfaces;

namespace Identity.Application.Services;

public sealed class ProofOfWorkService : IProofOfWorkService
{
    public bool IsSolved(byte[] challenge, ulong nonce, int difficulty)
    {
        var input = new byte[challenge.Length + sizeof(ulong)];
        challenge.CopyTo(input, 0);
        BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(challenge.Length), nonce);

        return LeadingZeroBits(SHA256.HashData(input)) >= difficulty;
    }

    private static int LeadingZeroBits(ReadOnlySpan<byte> hash)
    {
        var count = 0;
        foreach (var b in hash)
        {
            if (b == 0)
            {
                count += 8;
                continue;
            }

            return count + BitOperations.LeadingZeroCount(b) - 24;
        }

        return count;
    }
}
