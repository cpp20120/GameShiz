using BotFramework.Host;
using BotFramework.Sdk;
using Games.Darts;

namespace Games.Admin.Application.Services;

public interface IAdminService
{
    Task<int> UserSyncAsync(long callerId, CancellationToken ct);
    /// <param name="balanceScopeId">Chat where the /run pay is executed — coins apply to that wallet.</param>
    Task<PayResult?> PayAsync(long callerId, long targetUserId, long balanceScopeId, int amount, CancellationToken ct);
    Task<UserSummary?> GetUserAsync(long targetUserId, long balanceScopeId, CancellationToken ct);
    Task<ClearChatBetsResult> ClearChatBetsAsync(long callerId, long chatId, CancellationToken ct);
    Task<RenameResult> RenameAsync(string oldName, string newName, CancellationToken ct);
    void ReportNotAdmin(long userId);
    void ReportUserInfo(long callerId, string targetId);
}
