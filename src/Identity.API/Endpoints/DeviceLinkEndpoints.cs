using Identity.API.Common;
using Identity.Application.Features.Device.Commands.Link;
using Identity.Application.Features.Device.Queries.Link;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Endpoints;

internal sealed class DeviceLinkEndpoints : IEndpoint
{
    private sealed record ApproveLinkBody(string Approval);

    public void MapRoutes(IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("api/user/device/link")
            .WithTags("Device link");

        group.MapPost("/code", async (ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new CreateLinkCodeCommand { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId() });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces<CreateLinkCodeDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/", async ([FromBody] RedeemLinkCodeCommand cmd, ISender sender) =>
            {
                var result = await sender.Send(cmd);
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<RedeemLinkCodeDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{pendingLinkId:guid}/status", async (Guid pendingLinkId, [FromHeader(Name = "X-Poll-Secret")] string pollSecret, ISender sender) =>
            {
                var result = await sender.Send(new GetLinkStatusQuery { PendingLinkId = pendingLinkId, PollSecret = pollSecret });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .Produces<LinkStatusDto>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{pendingLinkId:guid}", async (Guid pendingLinkId, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new GetPendingLinkQuery { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId(), PendingLinkId = pendingLinkId });
                return result.Success ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces<PendingLinkDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{pendingLinkId:guid}/approve", async (Guid pendingLinkId, [FromBody] ApproveLinkBody body, ISender sender, HttpContext ctx) =>
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

        group.MapPost("/{pendingLinkId:guid}/reject", async (Guid pendingLinkId, ISender sender, HttpContext ctx) =>
            {
                var result = await sender.Send(new RejectLinkCommand { UserId = ctx.User.GetUserId(), DeviceId = ctx.User.GetDeviceId(), PendingLinkId = pendingLinkId });
                return result.Success ? Results.Ok() : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
