namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed record ChainDeviceDto(
    Guid Id,
    Guid ConnectionId,
    string PublicKey,
    string SignalIdentityKey,
    string? ApprovedBy,
    string? ApprovalSignature,
    bool Removed);
