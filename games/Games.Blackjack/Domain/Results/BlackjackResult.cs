namespace Games.Blackjack;

public sealed record BlackjackResult(BlackjackError Error, BlackjackSnapshot? Snapshot, int? StateMessageId = null);
