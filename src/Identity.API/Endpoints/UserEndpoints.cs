using Identity.API.Common;
using Identity.Application.Features.Device.Commands.Delete;
using Identity.Application.Features.Device.Commands.ApproveLink;
using Identity.Application.Features.Device.Commands.CreateLinkCode;
using Identity.Application.Features.Device.Commands.RedeemLinkCode;
using Identity.Application.Features.Device.Commands.RejectLink;
using Identity.Application.Features.Device.Queries.GetLinkStatus;
using Identity.Application.Features.Device.Queries.GetPendingLink;
using Identity.Application.Features.Device.Queries.GetConnectionIdByUser;
using Identity.Application.Features.Device.Queries.GetDevices;
using Identity.Application.Features.User.Commands.Delete;
using Identity.Application.Features.User.Commands.Recover;
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

        group.MapPost("/signup", async ([FromBody] SignupCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<SignupDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/recover", async ([FromBody] RecoverCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<RecoverDto>()
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

        group.MapPost("/device/link/code", async (ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new CreateLinkCodeCommand { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId() });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces<CreateLinkCodeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/device/link", async ([FromBody] RedeemLinkCodeCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<RedeemLinkCodeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/device/link/{pendingLinkId:guid}/status", async (Guid pendingLinkId, [FromHeader(Name = "X-Poll-Secret")] string pollSecret, ISender sender) =>
            {
                var result = await sender.Send(new GetLinkStatusQuery { PendingLinkId = pendingLinkId, PollSecret = pollSecret });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<LinkStatusDto>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/device/link/{pendingLinkId:guid}", async (Guid pendingLinkId, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new GetPendingLinkQuery { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId(), PendingLinkId = pendingLinkId });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces<PendingLinkDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/device/link/{pendingLinkId:guid}/approve", async (Guid pendingLinkId, [FromBody] ApproveLinkRequest body, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new ApproveLinkCommand
                {
                    UserId = ctx.User.GetUserId(),
                    DeviceId = ctx.User.GetDeviceId(),
                    PendingLinkId = pendingLinkId,
                    Approval = body.Approval,
                });
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/device/link/{pendingLinkId:guid}/reject", async (Guid pendingLinkId, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new RejectLinkCommand { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId(), PendingLinkId = pendingLinkId });
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
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
