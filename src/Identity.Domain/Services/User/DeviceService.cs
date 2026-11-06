using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Domain.Services.User;

public interface IDeviceService
{
    public Device RegisterDevice(Aggregates.User user, string devicePublicKey,
        string deviceAppVersion, DeviceType deviceType, DevicePlatform devicePlatform, string? approvedBy, string? approvalSignature);
}

public class DeviceService : IDeviceService
{
    public Device RegisterDevice(Aggregates.User user, string devicePublicKey, string deviceAppVersion, DeviceType deviceType, DevicePlatform devicePlatform, string? approvedBy, string? approvalSignature)
    {
        var device = new Device(Guid.NewGuid(), devicePublicKey, deviceAppVersion, deviceType, devicePlatform, user.Id, approvedBy, approvalSignature);

        user.AddDevice(device);

        return device;
    }
}
