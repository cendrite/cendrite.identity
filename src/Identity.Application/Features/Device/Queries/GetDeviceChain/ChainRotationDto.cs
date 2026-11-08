namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed record ChainRotationDto(
    Guid DeviceId,
    string OldAccountPublicKey,
    string NewAccountPublicKey,
    string Proof,
    string Approval,
    DateTime Created);
