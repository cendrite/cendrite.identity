namespace Identity.Application.Features.Device.Commands.RedeemLinkCode;

public sealed record RedeemLinkCodeDto(Guid PendingLinkId, string PollSecret, DateTime ExpiresAt);
