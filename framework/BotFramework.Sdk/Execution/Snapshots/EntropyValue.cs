using System.Collections.ObjectModel;

namespace BotFramework.Sdk.Execution;

public sealed class EntropyValue
{
    private readonly ReadOnlyDictionary<string, double> values;

    public EntropyValue(IEnumerable<KeyValuePair<string, double>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var copy = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Entropy names cannot be empty.", nameof(values));
            if (value is < 0 or >= 1 || double.IsNaN(value))
                throw new ArgumentException("Entropy values must be in [0, 1).", nameof(values));
            if (!copy.TryAdd(name, value))
                throw new ArgumentException($"Duplicate entropy name '{name}'.", nameof(values));
        }

        this.values = new ReadOnlyDictionary<string, double>(copy);
    }

    public static EntropyValue Empty { get; } = new([]);

    public IReadOnlyDictionary<string, double> Values => values;

    public double GetDouble(string name) => values.TryGetValue(name, out var value)
        ? value
        : throw new KeyNotFoundException($"Entropy value '{name}' was not supplied.");
}
