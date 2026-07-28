namespace Games.Darts.Application.Execution;

public sealed record DartsQueuedState(DartsRound? Round, int QueuedAhead);
