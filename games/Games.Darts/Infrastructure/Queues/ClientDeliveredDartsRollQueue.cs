namespace Games.Darts.Infrastructure.Queues;

/// <summary>
/// Remote composition marker: the BFF delivers the roll after PlaceBet returns,
/// so the backend must not retain an in-memory delivery job.
/// </summary>
public sealed class ClientDeliveredDartsRollQueue : IDartsRollQueue
{
    public void Enqueue(in DartsRollJob job)
    {
    }

    public ValueTask<DartsRollJob> ReadAsync(CancellationToken ct) =>
        ValueTask.FromException<DartsRollJob>(
            new InvalidOperationException("Client-delivered darts queue has no reader."));
}
