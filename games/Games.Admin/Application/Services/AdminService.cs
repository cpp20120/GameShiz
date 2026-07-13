
using System.Globalization;
using BotFramework.Host.Admin.Execution;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Sdk.Admin.Effects;
using Games.Admin.Application.Effects;

namespace Games.Admin.Application.Services;

public sealed partial class AdminService(
    IAdminStore store,
    IAdminEffectExecutor effects,
    IAnalyticsService analytics,
    ILogger<AdminService> logger,
    IMiniGameSessionStore? sessions = null,
    IMiniGameRollGateStore? rollGates = null) : IAdminService
{
    private IMiniGameSessionStore Sessions => sessions ?? NullMiniGameSessionStore.Instance;
    private IMiniGameRollGateStore RollGates => rollGates ?? NullMiniGameRollGateStore.Instance;

    /// <summary>Compatibility overload for callers that do not have a Telegram actor id.</summary>
    public Task<RenameResult> RenameAsync(string oldName, string newName, CancellationToken ct) =>
        RenameAsync(0, oldName, newName, ct);

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
        var newBalance = await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new(callerId, string.Create(CultureInfo.InvariantCulture, $"telegram:{callerId}")),
                "admin.pay",
                new { targetUserId, balanceScopeId, amount }),
            new AdminEffectPlan<int>(
                before?.Coins ?? 0,
                [new WalletAdjustmentAdminEffect(
                    targetUserId,
                    balanceScopeId,
                    amount,
                    "admin.pay",
                    DisplayName: displayName,
                    AllowNegative: false)],
                outputs => (int)outputs["balance"]!),
            ct).ConfigureAwait(false);

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
        return new PayResult(after.DisplayName, oldCoins, newBalance, amount);
    }

    public Task<UserSummary?> GetUserAsync(long targetUserId, long balanceScopeId, CancellationToken ct) =>
        store.FindUserAsync(targetUserId, balanceScopeId, ct);

    public async Task<ClearChatBetsResult> ClearChatBetsAsync(long callerId, long chatId, CancellationToken ct)
    {
        var deleted = await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new(callerId, string.Create(CultureInfo.InvariantCulture, $"telegram:{callerId}")),
                "admin.clearbets",
                new { chatId }),
            new AdminEffectPlan<IReadOnlyList<Games.Admin.Infrastructure.Models.PendingChatBet>>(
                [],
                [new ClearChatBetsAdminEffect(chatId)],
                outputs => (IReadOnlyList<Games.Admin.Infrastructure.Models.PendingChatBet>)outputs["bets"]!),
            ct).ConfigureAwait(false);
        foreach (var bet in deleted)
        {
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

    public async Task<RenameResult> RenameAsync(long callerId, string oldName, string newName, CancellationToken ct)
    {
        var existing = await store.GetOverrideAsync(oldName, ct);

        if (string.Equals(newName, "*", StringComparison.Ordinal))
        {
            if (existing == null) return new RenameResult(RenameOp.NoChange, oldName, newName);
            await effects.ExecuteAsync(
                new AdminExecutionEnvelope(
                    new(callerId, string.Create(CultureInfo.InvariantCulture, $"telegram:{callerId}")),
                    "admin.rename.clear",
                    new { oldName }),
                new AdminEffectPlan<bool>(
                    true,
                    [new DisplayNameOverrideAdminEffect(oldName, null)]),
                ct).ConfigureAwait(false);
            return new RenameResult(RenameOp.Cleared, oldName, newName);
        }

        await effects.ExecuteAsync(
            new AdminExecutionEnvelope(
                new(callerId, string.Create(CultureInfo.InvariantCulture, $"telegram:{callerId}")),
                "admin.rename.set",
                new { oldName, newName }),
            new AdminEffectPlan<bool>(
                true,
                [new DisplayNameOverrideAdminEffect(oldName, newName)]),
            ct).ConfigureAwait(false);
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
