using FakeItEasy;
using FluentAssertions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.Authentication.Commands.Logout;
using Identity.Domain.Aggregates;
using Identity.Domain.Common;
using Identity.Domain.Services.User;
using Microsoft.Extensions.Logging;

namespace Identity.Test.Unit.AuthenticationTest.CommandTest.Logout;

public class LogoutCommandHandlerTest
{
    private readonly IUserRepository _fakeRepo;
    private readonly IAuthenticationService _fakeAuthService;
    private readonly IUnitOfWork _fakeUnitOfWork;
    private readonly LogoutCommandHandler _sut;

    public LogoutCommandHandlerTest()
    {
        _fakeRepo = A.Fake<IUserRepository>();
        _fakeAuthService = A.Fake<IAuthenticationService>();
        _fakeUnitOfWork = A.Fake<IUnitOfWork>();
        _sut = new LogoutCommandHandler(_fakeRepo, _fakeAuthService, _fakeUnitOfWork, A.Fake<ILogger<LogoutCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var command = new LogoutCommand { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid() };
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns((User)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFound(command.Id));
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeAuthService.Logout(A<User>._, A<Guid>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_When_Valid_Command_Should_Return_Result_Ok()
    {
        // Arrange
        var user = new User(Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        var command = new LogoutCommand { Id = user.Id, DeviceId = Guid.NewGuid() };

        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _fakeAuthService.Logout(user, command.DeviceId)).DoesNothing();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).DoesNothing();

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        A.CallTo(() => _fakeAuthService.Logout(user, command.DeviceId)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeUnitOfWork.SaveChangesAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fakeRepo.GetSinleAsync(A<UserWithDevicesSpec>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}