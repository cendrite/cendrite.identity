namespace Identity.Application.Features.Device.Commands.CreateLinkCode;

public sealed record CreateLinkCodeDto(string Code, DateTime ExpiresAt);
