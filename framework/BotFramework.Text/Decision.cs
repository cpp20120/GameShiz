namespace BotFramework.Text;

public sealed record Decision
{
    public IReadOnlyList<IMessageEffect> Effects { get; init; } = [];
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public static Decision Empty { get; } = new();
}
