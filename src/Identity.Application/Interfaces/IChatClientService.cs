using Cendrite.Domain.Common;

namespace Identity.Application.Interfaces;

public interface IChatClientService
{
    Task<Result> CreateClientAsync(Guid userId, Guid deviceId, Guid connectionId, CancellationToken ct = default);
}
