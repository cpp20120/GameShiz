using BotFramework.Host;
using BotFramework.Sdk;
using Microsoft.Extensions.Options;

namespace Games.Redeem.Application.Services;

public interface IRedeemService
{
    Task<Guid> IssueAdminCodeAsync(long userId, CancellationToken ct, string? freeSpinGameId = null);
    Task<BeginRedeemResult> BeginRedeemAsync(
        long userId, long balanceScopeId, string displayName, string codeText, CancellationToken ct);
    Task<CompleteRedeemResult> CompleteRedeemAsync(long userId, long balanceScopeId, Guid codeGuid, CancellationToken ct);
    void ReportCaptcha(long userId, string codeText, string pattern, bool passed);
}
