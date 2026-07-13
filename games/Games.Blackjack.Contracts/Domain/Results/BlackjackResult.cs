namespace Games.Blackjack.Domain.Results;

public sealed record BlackjackResult(BlackjackError Error, BlackjackSnapshot? Snapshot, int? StateMessageId = null);
