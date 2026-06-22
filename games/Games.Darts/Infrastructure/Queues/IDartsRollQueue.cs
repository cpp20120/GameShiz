namespace Games.Darts.Infrastructure.Queues;

public interface IDartsRollQueue
{
    void Enqueue(in DartsRollJob job);
    ValueTask<DartsRollJob> ReadAsync(CancellationToken ct);
}
