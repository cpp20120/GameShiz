namespace Games.Blackjack.Application.Execution;

internal sealed record BlackjackSettlement(
    BlackjackSnapshot Snapshot,
    int Payout,
    BlackjackHandCompleted Completed,
    BlackjackHandClosed Closed);
