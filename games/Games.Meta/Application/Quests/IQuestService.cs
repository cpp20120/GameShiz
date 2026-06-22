namespace Games.Meta.Application.Quests;

public interface IQuestService
{
    Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct);
    Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(long chatId, long userId, CancellationToken ct);
    Task<QuestClaimResult?> ClaimAsync(long chatId, long userId, string displayName, string questId, CancellationToken ct);
}
