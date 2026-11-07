using Cendrite.Domain.Common;
using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Responses.Client;
using Identity.Application.Interfaces;
using Identity.Domain.Common;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Services;

public sealed class ChatClientService(
    IRequestClient<CreateClientMessage> clientClient,
    ILogger<ChatClientService> logger)
    : IChatClientService
{
    public async Task<Result> CreateClientAsync(Guid userId, Guid deviceId, Guid connectionId, CancellationToken ct = default)
    {
        var response = await clientClient.GetResponse<CreateClientResponse>(new CreateClientMessage
        {
            CorrelationId = Guid.NewGuid(),
            DeviceId = deviceId,
            ConnectionId = connectionId,
            UserId = userId
        }, ct);

        if (response.Message.IsSuccess)
            return Result.Ok();

        logger.LogError("Got a fail response from the Chat server attempting to create a Client for Device with ID {DeviceId}", deviceId);
        return Result.Fail(Errors.General.UnspecifiedError("Failed to create client"));
    }
}
