using AutoFixture;
using Cendrite.Message.Contracts.Enums;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Consumers;
using Identity.Domain.Aggregates;
using Identity.Domain.Services.User;
using MassTransit;
using Microsoft.Extensions.Logging;
using Identity.Test.Unit.Setup.Helpers;

namespace Identity.Test.Unit.UserTest.ConsumerTest;

public class RollbackUserDeleteConsumerTest
{
    private readonly IUserRepository _fakeRepo;
    private readonly IUserLifeCycleService _fakeLifeCycleServices;
    private readonly IUnitOfWork _fakeUnitOfWork;
    private readonly ILogger<RollbackUserDeleteConsumer> _fakeLogger;
    private readonly Fixture _fixture = new();
    private readonly RollbackUserDeleteConsumer _sut;

    public RollbackUserDeleteConsumerTest()
    {
        _fakeRepo = A.Fake<IUserRepository>();
        _fakeLifeCycleServices = A.Fake<IUserLifeCycleService>();
        _fakeUnitOfWork = A.Fake<IUnitOfWork>();
        _fakeLogger = A.Fake<ILogger<RollbackUserDeleteConsumer>>();
        _sut = new RollbackUserDeleteConsumer(_fakeRepo, _fakeLifeCycleServices, _fakeUnitOfWork, _fakeLogger);
    }

    [Fact]
    public async Task Consume_Valid_Message_Should_Revert_Soft_Delete()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"), UserTags.New());
        user.SetDelete();

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns(user);

        var message = _fixture.Build<UserDeleteFailedMessage>()
            .With(x => x.UserId, user.Id)
            .With(x => x.Services, [ServiceType.AuthenticationService])
            .Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<UserDeleteFailedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        await _sut.Consume(fakeConsumeContext);

        // Assert
        A.CallTo(() => _fakeLifeCycleServices.RevertSoftDelete(user)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Consume_Message_With_Invalid_ServiceType_Should_Do_Nothing()
    {
        // Arrange
        var message = _fixture.Build<UserDeleteFailedMessage>()
            .With(x => x.Services, [ServiceType.UserManagementService])
            .Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<UserDeleteFailedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        await _sut.Consume(fakeConsumeContext);

        // Assert
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeLifeCycleServices.RevertSoftDelete(A<User>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Consume_When_User_Does_Not_Exist_Should_Do_Nothing()
    {
        var message = _fixture.Build<UserDeleteFailedMessage>()
            .With(x => x.Services, [ServiceType.AuthenticationService])
            .Create();

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns<User?>(null);

        var fakeConsumeContext = A.Fake<ConsumeContext<UserDeleteFailedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        Func<Task> act = async () => await _sut.Consume(fakeConsumeContext);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
