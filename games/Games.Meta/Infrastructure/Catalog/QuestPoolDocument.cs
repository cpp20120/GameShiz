namespace Games.Meta;

internal sealed class QuestPoolDocument
{
    public List<QuestGame> Games { get; set; } = [];
    public List<QuestSlotDocument> Slots { get; set; } = [];
    public List<QuestDefinition> Definitions { get; set; } = [];
}
