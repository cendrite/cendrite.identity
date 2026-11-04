namespace Identity.Application.Features.User.Commands.Signup;

public sealed record SignupDto(Guid UserId, Guid DeviceId, Guid DeviceConnectionId);
