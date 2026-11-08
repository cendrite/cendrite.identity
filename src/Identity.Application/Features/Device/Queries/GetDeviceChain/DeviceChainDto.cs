namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed record DeviceChainDto(
    string UserTag,
    string AccountPublicKey,
    IReadOnlyList<ChainDeviceDto> Devices,
    IReadOnlyList<ChainRotationDto> Rotations);
