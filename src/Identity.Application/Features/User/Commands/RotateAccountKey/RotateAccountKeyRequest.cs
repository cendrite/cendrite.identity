namespace Identity.Application.Features.User.Commands.RotateAccountKey;

public sealed record RotateAccountKeyRequest(string NewAccountPublicKey, string Proof, string Approval);
