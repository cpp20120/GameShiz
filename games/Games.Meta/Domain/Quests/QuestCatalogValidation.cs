namespace Games.Meta;

public sealed record QuestCatalogValidation(
    int QuestCount,
    int SlotCount,
    int DefinitionCount,
    int DailyQuestCount,
    int WeeklyQuestCount);
