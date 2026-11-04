using Cendrite.Domain.Common;
using Identity.Domain.Abstractions;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.Events.User;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Aggregates;

public class User : AggregateRoot
{
    public Email? Email { get; init; }
    public Password? Password { get; private set; }
    public string? AccountPublicKey { get; private set; }
    public bool IsVerified { get; private set; }
    private readonly List<UserVerificationCode> _verificationCodes = [];
    private readonly List<Device> _devices = [];
    public IReadOnlyCollection<Device> Devices => _devices;
    public IReadOnlyCollection<UserVerificationCode> VerificationCodes => _verificationCodes;

    public User() : base(Guid.Empty) { } // For EF Core

    public User(Guid id, Email email, Password password, bool isVerified) : base(id)
    {
        Email = email;
        Password = password;
        IsVerified = isVerified;
    }

    public User(Guid id, string accountPublicKey) : base(id)
    {
        AccountPublicKey = accountPublicKey;
        IsVerified = true;
    }

    /// <summary>
    /// Adds a verification code to the user for the specified code type.
    /// <para>
    /// Marks any existing codes of the same type as used.
    /// </para>
    /// </summary>
    /// <param name="codeType"><see cref="UserVerificationCodeType"/></param>
    public void AddVerificationCode(UserVerificationCodeType codeType)
    {
        var existingCodes = VerificationCodes.Where(vc => vc.CodeType == codeType && !vc.Code.IsUsed);

        foreach (var vc in existingCodes)
        {
            vc.Code.Use();
        }

        _verificationCodes.Add(new UserVerificationCode(Guid.NewGuid(), userId: Id, codeType));
    }

    /// <summary>
    /// Returns the most recent active verification code of the specified type.
    /// <para>
    /// Returns null if no active codes are found.
    /// </para>
    /// </summary>
    /// <param name="codeType"><see cref="UserVerificationCodeType"/></param>
    /// <returns></returns>
    public UserVerificationCode? GetActiveVerificationCode(UserVerificationCodeType codeType)
    {
        return VerificationCodes.Where(vc => vc.CodeType == codeType && !vc.Code.IsUsed && vc.Code.ExpirationDate > DateTime.UtcNow).MaxBy(vc => vc.Code.ExpirationDate);
    }

    /// <summary>
    /// Verifies the user account with the provided verification code.
    /// </summary>
    /// <param name="verificationCode">Value representing the VerificationCode.Code <see cref="UserVerificationCode.Code"/></param>
    /// <returns>Result representing the verification result</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Result VerifyAccount(string verificationCode)
    {
        if (VerificationCodes.Count == 0)
            throw new InvalidOperationException("This chat user does not have a verification code");

        if (IsVerified)
            throw new InvalidOperationException("This chat user is already verified");

        var userVerificationCode = GetActiveVerificationCode(UserVerificationCodeType.EmailVerification);
        if (userVerificationCode is null) return Result.Fail(Errors.General.UnspecifiedError("Invalid verification code"));

        var verificationResult = userVerificationCode.Code.Verify(verificationCode);

        if (verificationResult.Success is false)
            return verificationResult;

        userVerificationCode.Code.Use();
        IsVerified = true;
        AddDomainEvent(new UserVerifiedEvent(Id));
        return Result.Ok();
    }


    /// <summary>
    /// Verifies the login with the provided verification code.
    /// </summary>
    /// <param name="loginVerificationCode">Value representing the VerificationCode.Code <see cref="UserVerificationCode.Code"/></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Result VerifyLogin(string loginVerificationCode)
    {
        if (VerificationCodes.Count == 0)
            throw new InvalidOperationException("This chat user does not have a verification code");

        var userVerificationCode = GetActiveVerificationCode(UserVerificationCodeType.Login);
        if (userVerificationCode is null) return Result.Fail(Errors.General.UnspecifiedError("Invalid verification code"));

        var verificationResult = userVerificationCode.Code.Verify(loginVerificationCode);

        if (verificationResult.Success is false)
            return verificationResult;

        userVerificationCode.Code.Use();
        return Result.Ok();
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
