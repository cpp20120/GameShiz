namespace Games.Poker.Application.Execution;

public sealed record PokerExecutionState(
    PokerTable? Table,
    List<PokerSeat> Seats,
    int? ActorBalance);
