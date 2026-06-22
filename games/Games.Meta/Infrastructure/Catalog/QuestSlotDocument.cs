namespace Games.Meta.Infrastructure.Catalog;

internal sealed class QuestSlotDocument
{
    public string Id { get; set; } = "";
    public string Period { get; set; } = "daily";
    public List<string> PoolTags { get; set; } = [];
    public int Count { get; set; } = 1;
    public int RepeatCooldownPeriods { get; set; } = 1;
}
