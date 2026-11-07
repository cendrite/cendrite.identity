using Identity.Domain.Abstractions;

namespace Identity.Domain.Entities;

public class AccountKeyRotation : Entity
{
    public Guid UserId { get; private init; }
    public Guid DeviceId { get; private init; }
    public string OldAccountPublicKey { get; private init; } = null!;
    public string NewAccountPublicKey { get; private init; } = null!;
    public string Proof { get; private init; } = null!;
    public string Approval { get; private init; } = null!;

    public AccountKeyRotation() : base(Guid.Empty) { } // For EF Core

    public AccountKeyRotation(Guid id, Guid userId, Guid deviceId, string oldAccountPublicKey, string newAccountPublicKey, string proof, string approval)
        : base(id)
    {
        UserId = userId;
        DeviceId = deviceId;
        OldAccountPublicKey = oldAccountPublicKey;
        NewAccountPublicKey = newAccountPublicKey;
        Proof = proof;
        Approval = approval;
    }
}
