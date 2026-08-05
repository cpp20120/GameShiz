namespace BotFramework.Text;

public sealed record PolicyDecision
{
    public required string PolicyId { get; init; }
    public IReadOnlyList<IMessageEffect> Effects { get; init; } = [];
    public IReadOnlyDictionary<string, object?> Values { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Stops evaluation of lower-priority policies. Already produced decisions are still composed.
    /// </summary>
    public bool IsTerminal { get; init; }
}
