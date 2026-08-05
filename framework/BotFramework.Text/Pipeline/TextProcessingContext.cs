namespace BotFramework.Text;

/// <summary>
/// Transport-neutral metadata associated with the text being processed.
/// </summary>
public sealed record TextProcessingContext
{
    public string? MessageId { get; init; }
    public string? Source { get; init; }
    public string? RequestId { get; init; }
    public string? CorrelationId { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    public bool TryGetProperty<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Properties.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public T GetRequiredProperty<T>(string key)
    {
        if (TryGetProperty<T>(key, out var value))
            return value!;

        throw new InvalidOperationException(
            $"Text processing property '{key}' is missing or is not assignable to '{typeof(T).FullName}'.");
    }
}
