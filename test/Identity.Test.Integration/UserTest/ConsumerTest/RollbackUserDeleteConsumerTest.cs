using AutoFixture;
using Cendrite.Message.Contracts.Enums;
using Cendrite.Message.Contracts.Messages.User;
using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Consumers;
using Identity.Domain.Aggregates;
using Identity.Domain.Services.User;
using Identity.Infrastructure.Persistence.Context;
using Identity.Test.Integration.Setup;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Integration.UserTest.ConsumerTest;

public class RollbackUserDeleteConsumerTest : IntegrationTestBase
{
    private readonly RollbackUserDeleteConsumer _sut;
    public RollbackUserDeleteConsumerTest(IntegrationTestFactory<Program, IdentityDbContext> factory) : base(factory)
    {
        var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IUserLifeCycleService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RollbackUserDeleteConsumer>>();
        _sut = new RollbackUserDeleteConsumer(repo, service, unitOfWork, logger);
    }

    [Fact]
    public async Task Consume_Valid_Message_Should_Revert_SoftDelete()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        user.SetDelete();
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        var message = Fixture.Build<UserDeleteFailedMessage>()
            .With(x => x.UserId, user.Id)
            .With(x => x.Services, [ServiceType.AuthenticationService])
            .Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<UserDeleteFailedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        await _sut.Consume(fakeConsumeContext);

        // Assert
        Db.User.AsNoTracking().FirstOrDefault()!.Deleted.Should().BeNull();
    }

    [Fact]
    public async Task Consume_Invalid_Message_Should_Not_Revert_SoftDelete()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        user.SetDelete();
        await Db.User.AddAsync(user);
        await Db.SaveChangesAsync();

        // Invalid ID
        var message = Fixture.Build<UserDeleteFailedMessage>()
            .With(x => x.Services, [ServiceType.AuthenticationService])
            .Create();

        var fakeConsumeContext = A.Fake<ConsumeContext<UserDeleteFailedMessage>>();
        A.CallTo(() => fakeConsumeContext.Message).Returns(message);

        // Act
        var act = async () => await _sut.Consume(fakeConsumeContext);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
        Db.User.AsNoTracking().FirstOrDefault()!.Deleted.Should().NotBeNull();
    }

}
