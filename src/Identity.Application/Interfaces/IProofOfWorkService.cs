namespace Identity.Application.Interfaces;

public interface IProofOfWorkService
{
    bool IsSolved(byte[] challenge, ulong nonce, int difficulty);
}
