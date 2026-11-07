using Identity.Domain.Abstractions;
using Identity.Domain.Entities;

namespace Identity.Domain.Aggregates;

public class User : AggregateRoot
{
    public string AccountPublicKey { get; private set; } = null!;
    private readonly List<Device> _devices = [];
    private readonly List<AccountKeyRotation> _accountKeyRotations = [];

    public IReadOnlyCollection<Device> Devices => _devices;
    public IReadOnlyCollection<AccountKeyRotation> AccountKeyRotations => _accountKeyRotations;

    public User() : base(Guid.Empty) { } // For EF Core

    public User(Guid id, string accountPublicKey) : base(id)
    {
        AccountPublicKey = accountPublicKey;
    }

    /// <summary>
    /// Add a device to the user.
    /// </summary>
    /// <param name="device"></param>
    public void AddDevice(Device device)
    {
        _devices.Add(device);
    }

    /// <summary>
    /// Rotate the account key for the user.
    /// </summary>
    /// <param name="deviceId">The ID of the device that is requesting the rotation</param>
    /// <param name="newAccountPublicKey">The new account public key</param>
    /// <param name="proof">The proof of the new account public key</param>
    /// <param name="approval">The approval of the new account public key</param>
    public AccountKeyRotation RotateAccountKey(Guid deviceId, string newAccountPublicKey, string proof, string approval)
    {
        var device = GetDevice(deviceId);
        var rotation = new AccountKeyRotation(Guid.NewGuid(), Id, device.Id, AccountPublicKey, newAccountPublicKey, proof, approval);

        _accountKeyRotations.Add(rotation);
        AccountPublicKey = newAccountPublicKey;

        return rotation;
    }

    /// <summary>
    /// Returns list of all active devices, or all devices when excludeDeleted is false.
    /// Devices where DeletedAt value is null <see cref="Entity.Deleted"/>
    /// </summary>
    /// <returns></returns>
    public List<Device> GetDevices(bool excludeDeleted = true)
    {
        return [.. Devices.Where(x => !excludeDeleted || x.Deleted is null)];
    }

    /// <summary>
    /// Returns the device with the specified deviceId.
    /// </summary>
    /// <param name="deviceId">The ID of the device to retreive</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Exception thrown if the device is marked as Deleted</exception>
    public Device GetDevice(Guid deviceId, bool includeDeleted = false)
    {
        if (includeDeleted)
            return Devices.FirstOrDefault(d => d.Id == deviceId) ?? throw new InvalidOperationException("Device not found");

        return Devices.FirstOrDefault(d => d.Id == deviceId && d.Deleted is null) ?? throw new InvalidOperationException("Device not found");
    }

}
