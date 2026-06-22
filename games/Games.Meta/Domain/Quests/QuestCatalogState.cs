namespace Games.Meta.Domain.Quests;

internal sealed record QuestCatalogState(
    IReadOnlyList<QuestCandidate> Candidates,
    IReadOnlyList<QuestSlot> Slots,
    IReadOnlyList<QuestTemplate> All);
