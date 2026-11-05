using Identity.Application.Abstractions;
using Identity.Application.Contracts.Repository;
using Identity.Application.Features.User.Commands.Signup;
using Identity.Test.Architecture.Helpers;
using NetArchTest.Rules;

namespace Identity.Test.Architecture;

public class ApplicationTest
{
    [Fact]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(SignupCommandHandler).Assembly)
            .That()
            .ResideInNamespace("Cendrite.ChatUser.Application")
            .ShouldNot()
            .HaveDependencyOn("Cendrite.ChatUser.Infrastructure")
            .GetResult();

        result.ShouldBeSuccessful("Application project should not reference Infrastructure project");
    }

    [Fact]
    public void Application_Should_Not_Reference_Presentation()
    {
        var result = Types.InAssembly(typeof(SignupCommandHandler).Assembly)
            .That()
            .ResideInNamespace("Cendrite.ChatUser.Application")
            .ShouldNot()
            .HaveDependencyOn("Cendrite.ChatUser.API")
            .GetResult();

        result.ShouldBeSuccessful("Application project should not reference Presentation project");
    }

    [Fact]
    public void All_Repositories_Should_Inherit_From_IRepository()
    {
        var result = Types.InAssembly(typeof(IUserRepository).Assembly)
            .That()
            .HaveNameEndingWith("Repository")
            .Should()
            .HaveDependencyOn("Identity.Application.Contracts")
            .GetResult();

        result.ShouldBeSuccessful("All repositories should inherit from IRepository<T>");
    }

    [Fact]
    public void All_Repositories_Should_Reference_Domain()
    {
        var result = Types.InAssembly(typeof(IUserRepository).Assembly)
            .That()
            .HaveNameEndingWith("Repository")
            .Should()
            .HaveDependencyOn("Identity.Domain")
            .GetResult();

        result.ShouldBeSuccessful("All repositories should reference Domain project");
    }

    [Fact]
    public void All_Commands_Should_Implement_ICommand()
    {
        var result = Types.InAssembly(typeof(SignupCommand).Assembly)
            .That()
            .HaveNameEndingWith("Command")
            .And().DoNotHaveName("ICommand")
            .Should()
            .ImplementInterface(typeof(ICommand))
            .Or()
            .ImplementInterface(typeof(ICommand<>))
            .GetResult();

        result.ShouldBeSuccessful("All commands or queries should implement ICommand<T> or ICommand");
    }

    [Fact]
    public void All_Queries_Should_Implement_IQuery()
    {
        var result = Types.InAssembly(typeof(SignupCommand).Assembly)
            .That()
            .HaveNameEndingWith("Query")
            .Should()
            .ImplementInterface(typeof(IQuery<>))
            .GetResult();

        result.ShouldBeSuccessful("All commands or queries should implement IQuery<T>");
    }

    [Fact]
    public void All_CommandHandlers_Should_Implement_ICommandHandler()
    {
        var result = Types.InAssembly(typeof(SignupCommand).Assembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .GetResult();

        result.ShouldBeSuccessful("All command handlers or query handlers should implement ICommandHandler<>");
    }
}