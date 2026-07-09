
using System.Globalization;

namespace Games.Admin.Application.Services;

public sealed partial class AdminService(
    IAdminStore store,
    IEconomicsService economics,
    IAnalyticsService analytics,
    ILogger<AdminService> logger,
    IMiniGameSessionStore? sessions = null,
    IMiniGameRollGateStore? rollGates = null) : IAdminService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;
    private IMiniGameRollGateStore RollGates => rollGates ?? NullMiniGameRollGateStore.Instance;

    public async Task<int> UserSyncAsync(long callerId, CancellationToken ct)
    {
        var users = await store.ListUsersAsync(ct);

        foreach (var u in users)
        {
            analytics.Track("admin", "user_map", new Dictionary<string, object?>
(StringComparer.Ordinal)
            {
                ["user_id"] = u.TelegramUserId,
                ["display_name"] = u.DisplayName,
            });
        }

        analytics.Track("admin", "command", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["command"] = "usersync",
            ["caller_id"] = callerId,
            ["count"] = users.Count,
        });

        LogUserSync(callerId, users.Count);
        return users.Count;
    }

    public async Task<PayResult?> PayAsync(
        long callerId, long targetUserId, long balanceScopeId, int amount, CancellationToken ct)
    {
        var before = await store.FindUserAsync(targetUserId, balanceScopeId, ct);
        var displayName = before?.DisplayName ?? string.Create(CultureInfo.InvariantCulture, $"User ID: {targetUserId}");
        await economics.EnsureUserAsync(targetUserId, balanceScopeId, displayName, ct);

        switch (amount)
        {
            case > 0:
                await economics.CreditAsync(targetUserId, balanceScopeId, amount, "admin.pay", ct);
                break;
            case < 0:
                await economics.DebitAsync(targetUserId, balanceScopeId, -amount, "admin.pay", ct);
                break;
        }

        var after = await store.FindUserAsync(targetUserId, balanceScopeId, ct);
        if (after == null) return null;

        analytics.Track("admin", "command", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["command"] = "pay",
            ["caller_id"] = callerId,
            ["target_user_id"] = targetUserId,
            ["amount"] = amount,
        });

        var oldCoins = before?.Coins ?? 0;
        return new PayResult(after.DisplayName, oldCoins, after.Coins, amount);
    }

    public Task<UserSummary?> GetUserAsync(long targetUserId, long balanceScopeId, CancellationToken ct) =>
        store.FindUserAsync(targetUserId, balanceScopeId, ct);

    public async Task<ClearChatBetsResult> ClearChatBetsAsync(long callerId, long chatId, CancellationToken ct)
    {
        var deleted = await store.DeletePendingMiniGameBetsAsync(chatId, ct);
        foreach (var bet in deleted)
        {
            await economics.CreditAsync(bet.UserId, bet.ChatId, bet.Amount, $"admin.clearbets.{bet.GameId}", ct);
            BotMiniGameSession.ClearCompletedRound(bet.UserId, bet.ChatId, bet.GameId);
            await Sessions.ClearCompletedRoundAsync(bet.UserId, bet.ChatId, bet.GameId, ct);
            switch (bet.GameId)
            {
                case MiniGameIds.DiceCube:
                    BotMiniGameRollGate.Clear("dicecube", bet.UserId, bet.ChatId);
                    await RollGates.ClearAsync("dicecube", bet.UserId, bet.ChatId, ct);
                    break;
                case MiniGameIds.Football:
                    BotMiniGameRollGate.Clear("football", bet.UserId, bet.ChatId);
                    await RollGates.ClearAsync("football", bet.UserId, bet.ChatId, ct);
                    break;
                case MiniGameIds.Basketball:
                    BotMiniGameRollGate.Clear("basketball", bet.UserId, bet.ChatId);
                    await RollGates.ClearAsync("basketball", bet.UserId, bet.ChatId, ct);
                    break;
                case MiniGameIds.Bowling:
                    BotMiniGameRollGate.Clear("bowling", bet.UserId, bet.ChatId);
                    await RollGates.ClearAsync("bowling", bet.UserId, bet.ChatId, ct);
                    break;
                case MiniGameIds.Darts:
                    BotMiniGameRollGate.Clear("darts", bet.UserId, bet.ChatId);
                    await RollGates.ClearAsync("darts", bet.UserId, bet.ChatId, ct);
                    if (bet.BotMessageId is { } botMessageId)
                    {
                        BotMiniGameDiceOwner.MarkCompleted(bet.ChatId, botMessageId);
                    }
                    break;
            }
        }

        var refunded = deleted.Sum(x => x.Amount);
        analytics.Track("admin", "command", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["command"] = "clearbets",
            ["caller_id"] = callerId,
            ["chat_id"] = chatId,
            ["cleared_count"] = deleted.Count,
            ["total_refunded"] = refunded,
        });
        return new ClearChatBetsResult(deleted.Count, refunded);
    }

    public async Task<RenameResult> RenameAsync(string oldName, string newName, CancellationToken ct)
    {
        var existing = await store.GetOverrideAsync(oldName, ct);

        if (string.Equals(newName, "*", StringComparison.Ordinal))
        {
            if (existing == null) return new RenameResult(RenameOp.NoChange, oldName, newName);
            await store.DeleteOverrideAsync(oldName, ct);
            return new RenameResult(RenameOp.Cleared, oldName, newName);
        }

        await store.UpsertOverrideAsync(oldName, newName, ct);
        return new RenameResult(RenameOp.Set, oldName, newName);
    }

    public void ReportNotAdmin(long userId)
    {
        analytics.Track("admin", "command", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["command"] = "not_admin",
            ["caller_id"] = userId,
            ["type"] = "insufficient_permissions",
        });
    }

    public void ReportUserInfo(long callerId, string targetId)
    {
        analytics.Track("admin", "command", new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["command"] = "userinfo",
            ["caller_id"] = callerId,
            ["target_id"] = targetId,
        });
    }

    [LoggerMessage(LogLevel.Information, "admin.usersync caller={CallerId} count={Count}")]
    partial void LogUserSync(long callerId, int count);
}
