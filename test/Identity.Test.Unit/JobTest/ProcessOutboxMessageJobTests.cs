
using Identity.Application.Contracts.Repository;
using FakeItEasy;
using Identity.Domain.Abstractions;
using Identity.Domain.Events.User;
using Identity.Infrastructure.Jobs;
using Identity.Infrastructure.Persistence.Outbox;
using Identity.Infrastructure.Persistence.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;

namespace Cendrite.Authentication.Test.Unit.JobTest;

public class ProcessOutboxMessageJobTests
{
    private readonly IOutboxRepository _fakeOutboxRepository;
    private readonly IPublisher _fakePublisher;
    private readonly IUnitOfWork _fakeUnitOfWork;
    private readonly ProcessOutboxMessageJob _sut;
    private readonly IJobExecutionContext _fakeJobContext;

    public ProcessOutboxMessageJobTests()
    {
        _fakeOutboxRepository = A.Fake<IOutboxRepository>();
        _fakePublisher = A.Fake<IPublisher>();
        var fakeLogger = A.Fake<ILogger<ProcessOutboxMessageJob>>();
        _fakeUnitOfWork = A.Fake<IUnitOfWork>();
        _fakeJobContext = A.Fake<IJobExecutionContext>();
        _sut = new ProcessOutboxMessageJob(_fakeOutboxRepository, _fakePublisher, fakeLogger, _fakeUnitOfWork);
    }

    [Fact]
    public async Task Execute_WhenNoMessages_ShouldNotProcessAnyMessage()
    {
        // Arrange
        A.CallTo(() => _fakeOutboxRepository.GetUnprocessedAsync(20))
            .Returns([]);

        // Act
        await _sut.Execute(_fakeJobContext);

        // Assert
        A.CallTo(() => _fakeOutboxRepository.MarkAsProcessedAsync(A<OutboxMessage>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => _fakePublisher.Publish(A<IDomainEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Execute_WhenValidMessages_ShouldProcessAndMarkAsProcessed()
    {
        // Arrange
        var userCreatedEvent = new UserDeletedEvent(Guid.NewGuid());

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(UserDeletedEvent).AssemblyQualifiedName!,
            Content = JsonConvert.SerializeObject(userCreatedEvent)
        };

        A.CallTo(() => _fakeOutboxRepository.GetUnprocessedAsync(20))
            .Returns([message]);

        A.CallTo(() => _fakePublisher.Publish(A<UserDeletedEvent>.That.Matches(ev => ev.UserId == userCreatedEvent.UserId), A<CancellationToken>.Ignored))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.Execute(_fakeJobContext);

        // Assert
        A.CallTo(() => _fakePublisher.Publish(A<IDomainEvent>._, A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeOutboxRepository.MarkAsProcessedAsync(message))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Execute_WhenDomainEventTypeIsInvalid_ShouldLogErrorAndSkipMessage()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "InvalidTypeName",
            Content = JsonConvert.SerializeObject(new UserDeletedEvent(Guid.NewGuid()))
        };

        A.CallTo(() => _fakeOutboxRepository.GetUnprocessedAsync(20)).Returns([message]);

        // Act
        await _sut.Execute(_fakeJobContext);

        // Assert
        A.CallTo(() => _fakePublisher.Publish(A<IDomainEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => _fakeOutboxRepository.MarkAsProcessedAsync(message)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Execute_WhenDeserializationFails_ShouldLogErrorAndSkipMessage()
    {
        // Arrange
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(IDomainEvent).AssemblyQualifiedName!,
            Content = "InvalidJsonContent"
        };

        A.CallTo(() => _fakeOutboxRepository.GetUnprocessedAsync(20)).Returns([message]);

        // Act
        await _sut.Execute(_fakeJobContext);

        // Assert
        A.CallTo(() => _fakePublisher.Publish(A<IDomainEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => _fakeOutboxRepository.MarkAsProcessedAsync(message)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Execute_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        A.CallTo(() => _fakeOutboxRepository.GetUnprocessedAsync(20)).Throws(new Exception("Database connection failed"));

        // Act
        await _sut.Execute(_fakeJobContext);

        // Assert
        A.CallTo(() => _fakePublisher.Publish(A<IDomainEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => _fakeOutboxRepository.MarkAsProcessedAsync(A<OutboxMessage>.Ignored)).MustNotHaveHappened();
    }
}
