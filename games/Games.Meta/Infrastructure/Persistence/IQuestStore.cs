namespace Games.Meta.Infrastructure.Persistence;

public interface IQuestStore
{
    Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        CancellationToken ct);

    Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        CancellationToken ct);

    Task<QuestClaimResult?> TryMarkClaimedAsync(
        MetaSeason season,
        long chatId,
        long userId,
        string questId,
        DateTimeOffset now,
        CancellationToken ct);
}
