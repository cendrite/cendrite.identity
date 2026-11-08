using Identity.Application.Abstractions;

namespace Identity.Application.Features.Device.Queries.GetDeviceChain;

public sealed record GetDeviceChainQuery : IQuery<DeviceChainDto>
{
    public required Guid ConnectionId { get; init; }
}
