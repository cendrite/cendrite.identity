using Identity.API.Common;
using Identity.Application.Features.Device.Commands.Create;
using Identity.Application.Features.Device.Commands.Delete;
using Identity.Application.Features.Device.Queries.GetConnectionIdByUser;
using Identity.Application.Features.Device.Queries.GetDevices;
using Identity.Application.Features.User.Commands.Create;
using Identity.Application.Features.User.Commands.Delete;
using Identity.Application.Features.User.Commands.Signup;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Endpoints;

internal sealed class UserEndpoints : IEndpoint
{
    public void MapRoutes(IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("api/user")
            .WithTags("User");

        group.MapPost("/", async ([FromBody] CreateUserCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<CreateUserDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/signup", async ([FromBody] SignupCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<SignupDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("/", async (ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new DeleteUserCommand { Id = ctx.User.GetUserId() });
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/device", async ([FromBody] CreateDeviceCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<CreateDeviceDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/devices", async (ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new GetDevicesQuery { UserId = ctx.User.GetUserId() });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces<GetDevicesDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/device/{targetDeviceId}", async (Guid targetDeviceId, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new DeleteDeviceCommand { TargetDeviceId = targetDeviceId, UserId = ctx.User.GetUserId() });
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/device/connectionid", async (ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new GetConnectionIdsByUserQuery { TenantId = ctx.User.GetUserId() });
                if (!result.Success) return result.ToProblemDetails();

                return result.Value!.ConnectionIds.Count != 0 ? Results.Ok(result.Value) : Results.NoContent();
            })
            .RequireAuthorization()
            .Produces<GetConnectionIdsByUserDto>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
