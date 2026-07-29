using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using ChatAdministration.Domain.Effects;

namespace ChatAdministration.Telegram.Infrastructure;

internal static class ModerationMetrics
{
    private static readonly Meter Meter = new("CasinoShiz.ChatAdministration", "1.0.0");
    private static readonly ConcurrentDictionary<string, Counter<long>> Counters = new(StringComparer.Ordinal);

    public static void Record(EmitMetricEffect effect)
    {
        var counter = Counters.GetOrAdd(effect.Name, static name => Meter.CreateCounter<long>(name));
        counter.Add(1, effect.Labels.Select(label => new KeyValuePair<string, object?>(label.Key, label.Value)).ToArray());
    }
}
