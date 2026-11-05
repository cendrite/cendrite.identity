using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Commands.Delete;
using Identity.Domain.Aggregates;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.UserTest.CommandTest.DeleteTest;

public class DeleteUserCommandHandlerTest
{
    private readonly IUserRepository _fakeUserRepository;
    private readonly IUnitOfWork _fakeUnitOfWork;
    private readonly IUserLifeCycleService _fakeUserLifeCycleServices;
    private readonly DeleteUserCommandHandler _sut;

    public DeleteUserCommandHandlerTest()
    {
        _fakeUserRepository = A.Fake<IUserRepository>();
        _fakeUnitOfWork = A.Fake<IUnitOfWork>();
        _fakeUserLifeCycleServices = A.Fake<IUserLifeCycleService>();
        var fakeLogger = A.Fake<ILogger<DeleteUserCommandHandler>>();
        _sut = new DeleteUserCommandHandler(_fakeUserRepository, _fakeUnitOfWork, _fakeUserLifeCycleServices, fakeLogger);
    }

    [Fact]
    public async Task Handle_When_No_User_Found_Should_Return_ResultFail()
    {
        // Arrange
        var command = new DeleteUserCommand { Id = Guid.NewGuid() };

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns<User?>(null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        A.CallTo(() => _fakeUserLifeCycleServices.IsUserDeleted(A<User>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUserLifeCycleServices.SoftDelete(A<User>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();

    }

    [Fact]
    public async Task Handle_When_User_Already_Marked_As_Deleted_Should_Return_ResultFail()
    {
        // Arrange
        var command = new DeleteUserCommand { Id = Guid.NewGuid() };

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns(user);

        A.CallTo(() => _fakeUserLifeCycleServices.IsUserDeleted(user)).Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        A.CallTo(() => _fakeUserLifeCycleServices.SoftDelete(user)).MustNotHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_User_Deleted_Should_Return_ResultOk()
    {
        //Arrage
        var command = new DeleteUserCommand { Id = Guid.NewGuid() };

        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        A.CallTo(() => _fakeUserRepository.GetSinleAsync(A<UserSpec>._, A<CancellationToken>._)).Returns(user);

        A.CallTo(() => _fakeUserLifeCycleServices.IsUserDeleted(user)).Returns(false);

        //Act
        var result = await _sut.Handle(command, CancellationToken.None);

        //Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeUserLifeCycleServices.SoftDelete(user)).MustHaveHappened();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustHaveHappened();
    }
}
