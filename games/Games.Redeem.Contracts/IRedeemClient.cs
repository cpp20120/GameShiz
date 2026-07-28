namespace Games.Redeem.Contracts;

public interface IRedeemClient
{
    Task<Guid> IssueAdminCodeAsync(long userId, string? freeSpinGameId, CancellationToken ct);
    Task<BeginRedeemResponse> BeginAsync(
        long userId, long balanceScopeId, string displayName, string codeText, CancellationToken ct);
    Task<bool> VerifyCaptchaAsync(long userId, Guid codeGuid, int chosenId, CancellationToken ct);
    Task<CompleteRedeemResponse> CompleteAsync(
        long userId, long balanceScopeId, Guid codeGuid, CancellationToken ct);
}
