using Cendrite.Message.Contracts.Messages.Client;
using Cendrite.Message.Contracts.Responses.Client;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.ServiceTest;

public class ChatClientServiceTest
{
    private readonly IRequestClient<CreateClientMessage> _fakeClient = A.Fake<IRequestClient<CreateClientMessage>>();
    private readonly ChatClientService _sut;

    public ChatClientServiceTest()
    {
        _sut = new ChatClientService(_fakeClient, A.Fake<ILogger<ChatClientService>>());
    }

    private void RespondFromChatServer(bool isSuccess)
    {
        var response = A.Fake<Response<CreateClientResponse>>();
        A.CallTo(() => response.Message).Returns(new CreateClientResponse { CorrelationId = Guid.NewGuid(), IsSuccess = isSuccess });
        A.CallTo(() => _fakeClient.GetResponse<CreateClientResponse>(A<CreateClientMessage>._, A<CancellationToken>._, A<RequestTimeout>._))
            .Returns(response);
    }

    [Fact]
    public async Task CreateClientAsync_Should_Send_The_Device_And_Succeed_When_ChatServer_Agrees()
    {
        // Arrange
        var (userId, deviceId, connectionId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        RespondFromChatServer(isSuccess: true);

        // Act
        var result = await _sut.CreateClientAsync(userId, deviceId, connectionId);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeClient.GetResponse<CreateClientResponse>(
                A<CreateClientMessage>.That.Matches(m => m.UserId == userId && m.DeviceId == deviceId && m.ConnectionId == connectionId),
                A<CancellationToken>._, A<RequestTimeout>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateClientAsync_When_ChatServer_Refuses_Should_Fail()
    {
        // Arrange
        RespondFromChatServer(isSuccess: false);

        // Act
        var result = await _sut.CreateClientAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
    }
}
