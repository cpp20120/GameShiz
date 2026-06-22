namespace Games.Meta;

public interface IQuestCatalog
{
    IReadOnlyList<QuestTemplate> All { get; }
    void Reload();

    IReadOnlyList<QuestTemplate> ActiveFor(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null);

    IEnumerable<QuestTemplate> Matching(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        QuestPlayerProgress? progress = null);

    QuestTemplate? FindActive(
        MetaSeason season,
        long chatId,
        long userId,
        string questId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null);
}
