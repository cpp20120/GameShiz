namespace Games.Meta;

public static class QuestRegistry
{
    public static IReadOnlyList<QuestTemplate> All => JsonQuestCatalog.Default.All;

    public static IReadOnlyList<QuestTemplate> ActiveFor(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        QuestPlayerProgress? progress = null) => JsonQuestCatalog.Default.ActiveFor(season, chatId, userId, now, progress);

    public static IEnumerable<QuestTemplate> Matching(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        QuestPlayerProgress? progress = null) => JsonQuestCatalog.Default.Matching(season, chatId, userId, ev, progress);

    public static IEnumerable<QuestTemplate> Matching(GameCompletedMetaEvent ev)
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(ev.OccurredAt);
        var season = new MetaSeason(0, "default", now.AddYears(-1), now.AddYears(1), "active", "{}");
        return Matching(season, ev.ChatId, ev.UserId, ev);
    }

    public static int DeltaFor(QuestTemplate quest, GameCompletedMetaEvent ev) => JsonQuestCatalog.DeltaFor(quest, ev);

    public static string PeriodKey(QuestTemplate quest, DateTimeOffset now) => JsonQuestCatalog.PeriodKey(quest, now);
}
