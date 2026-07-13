namespace Games.Redeem.Contracts;

public enum RedeemClientError
{
    None,
    InvalidCode,
    AlreadyRedeemed,
    SelfRedeem,
    NoUser,
}

public sealed record RedeemCaptchaItem(string Text, int Data);
public sealed record RedeemCaptchaChallenge(string Pattern, IReadOnlyList<RedeemCaptchaItem> Items);
public sealed record BeginRedeemResponse(
    RedeemClientError Error,
    Guid CodeGuid = default,
    RedeemCaptchaChallenge? Captcha = null);
public sealed record CompleteRedeemResponse(RedeemClientError Error, string FreeSpinGameId = "");

public interface IRedeemClient
{
    Task<Guid> IssueAdminCodeAsync(long userId, string? freeSpinGameId, CancellationToken ct);
    Task<BeginRedeemResponse> BeginAsync(
        long userId, long balanceScopeId, string displayName, string codeText, CancellationToken ct);
    Task<bool> VerifyCaptchaAsync(long userId, Guid codeGuid, int chosenId, CancellationToken ct);
    Task<CompleteRedeemResponse> CompleteAsync(
        long userId, long balanceScopeId, Guid codeGuid, CancellationToken ct);
}
