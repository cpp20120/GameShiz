using System.Security.Cryptography;
using System.Text;
using BotFramework.Contracts.Operations;

namespace BotFramework.Host.Events.Replay;

public sealed class ReadOnlyEventReplayService(IEventStore eventStore, IEventSerializer serializer)
    : IReadOnlyEventReplayService
{
    public async Task<EventReplayReport> ReplayAsync(string streamId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(streamId))
            throw new ArgumentException("A stream ID is required.", nameof(streamId));

        var events = await eventStore.LoadAsync(streamId, ct);
        var steps = new List<ReplayStep>(events.Count);
        long? firstIncompatible = null;
        foreach (var stored in events)
        {
            string? diagnostic = null;
            var compatible = true;
            try
            {
                if (serializer.Deserialize(stored.EventType, stored.PayloadJson) is null)
                    throw new InvalidOperationException("The event type is not registered.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                compatible = false;
                diagnostic = exception.Message;
                firstIncompatible ??= stored.Version;
            }

            steps.Add(new(stored.Version, stored.EventType, stored.OccurredAt, compatible,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stored.PayloadJson))).ToLowerInvariant(),
                diagnostic));
        }

        return new(streamId, steps, firstIncompatible,
            firstIncompatible is null ? null : $"First incompatible event is version {firstIncompatible}.");
    }
}
