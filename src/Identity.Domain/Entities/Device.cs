using Cendrite.Domain.Common;
using Identity.Domain.Abstractions;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

public class Device : Entity
{
    public string Name { get; private set; } = null!;
    public string PublicKey { get; init; } = null!;
    public Guid ConnectionId { get; init; }
    public DateTime? LastSeen { get; private set; }
    public DeviceType Type { get; init; }
    public DevicePlatform Platform { get; init; }
    public string AppVersion { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public string? ApprovedBy { get; private init; }
    public string? ApprovalSignature { get; private init; }

    public Device() : base(Guid.Empty) { } // For EF Core

    public Device(Guid id,
        string publicKey,
        string appVersion,
        DeviceType type,
        DevicePlatform platform,
        Guid userId,
        string? approvedBy = null,
        string? approvalSignature = null) : base(id)
    {
        Name = GenerateName(platform);
        PublicKey = publicKey;
        AppVersion = appVersion;
        UserId = userId;
        Type = type;
        Platform = platform;
        ConnectionId = Guid.NewGuid();
        ApprovedBy = approvedBy;
        ApprovalSignature = approvalSignature;
    }

    /// <summary>
    /// Adds a default name to the device based on the <see cref="DevicePlatform"/>.
    /// </summary>
    /// <param name="platform"><see cref="DevicePlatform"/></param>
    /// <returns>A string representing the name generating based on the <see cref="DevicePlatform"/></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static string GenerateName(DevicePlatform platform)
    {
        return platform switch
        {
            DevicePlatform.Android => "Mobile.Android",
            DevicePlatform.iOS => "Mobile.iOS",
            DevicePlatform.Windows => "PC.Windows",
            DevicePlatform.MacOS => "PC.MacOS",
            DevicePlatform.Linux => "PC.Linux",
            DevicePlatform.Unknown => "Device.Unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };
    }

    /// <summary>
    /// Renames the device.
    /// </summary>
    public void Rename(string name)
    {
        Name = name.Trim();
    }

    /// <summary>
    /// Sets the time the device was last seen.
    /// </summary>
    public void SetLastSeen()
    {
        LastSeen = DateTime.UtcNow;
    }
}
