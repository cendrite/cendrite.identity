using System.Reflection;
using FluentValidation;
using Identity.Application.Behavior;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Application.Configuration;

public static class ApplicationExtensions
{
    public static void AddApplication(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly);
        // Options are read by the source generator at compile time, so they must stay inline constants
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(ValidationBehavior<,>), typeof(ExceptionBehavior<,>)];
        });
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<IVerifyNonceService, VerifyNonceService>();
        services.AddScoped<IProofOfWorkService, ProofOfWorkService>();
        services.AddScoped<IChatClientService, ChatClientService>();
        services.AddScoped<IUserTagGenerator, UserTagGenerator>();
    }
}
