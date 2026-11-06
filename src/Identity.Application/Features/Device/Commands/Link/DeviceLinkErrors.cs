using Identity.Domain.Common;

namespace Identity.Application.Features.Device.Commands.Link;

internal static class DeviceLinkErrors
{
    public static readonly Error LinkFailed = Errors.General.UnspecifiedError("Linking failed");
}
