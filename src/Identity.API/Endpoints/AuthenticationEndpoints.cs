using Identity.API.Common;
using Identity.Application.Features.Authentication.Commands.Login;
using Identity.Application.Features.Authentication.Commands.Logout;
using Identity.Application.Features.Authentication.Commands.VerifyNonce;
using Identity.Application.Features.Authentication.Queries.GetNonce;
using Identity.Application.Features.Authentication.Queries.GetSignupChallenge;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Endpoints;

internal sealed class AuthenticationEndpoints : IEndpoint
{
    public void MapRoutes(IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("api/authentication")
            .WithTags("Authentication");

        group.MapPost("/login", async ([FromBody] LoginCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<LoginDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (ISender sender, HttpContext ctx) =>
            {
                var cmd = new LogoutCommand { Id = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId() };
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/nonce", async ([AsParameters] GetNonceQuery query, ISender sender) =>
            {
                var result = await sender.Send(query);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<GetNonceDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/signup-challenge", async (ISender sender) =>
            {
                var result = await sender.Send(new GetSignupChallengeQuery());
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<GetSignupChallengeDto>();

        group.MapPost("/verify-nonce", async ([FromBody] VerifyNonceCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<VerifyNonceDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
