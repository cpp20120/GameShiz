namespace Games.Darts.Application.Jobs;

/// <summary>
/// Delivers a queued native-dice roll. The backend job depends on this port;
/// Telegram and future transports provide the implementation.
/// </summary>
public interface IDartsRollDelivery
{
    Task SendAsync(DartsRollJob job, CancellationToken ct);
}
