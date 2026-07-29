using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class ModerationAnalyticsService(IChatAdministrationStore store)
{
    public async Task<ModerationAnalytics?> ExecuteAsync(
        ChatId chatId,
        UserId actorUserId,
        ChatMemberRole actorRole,
        string displayName,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(chatId, actorUserId, actorUserId, actorRole, actorRole, displayName, displayName, ct);
        return AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.AnalyticsView)
            ? await store.LoadAnalyticsAsync(chatId, ct)
            : null;
    }
}
