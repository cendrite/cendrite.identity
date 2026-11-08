namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed record DeviceChainDto(
    Guid UserId,
    string AccountPublicKey,
    IReadOnlyList<ChainDeviceDto> Devices,
    IReadOnlyList<ChainRotationDto> Rotations);
