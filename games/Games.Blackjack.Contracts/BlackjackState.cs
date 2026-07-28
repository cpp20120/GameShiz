using Games.Blackjack.Domain.Results;

namespace Games.Blackjack.Contracts;

public sealed record BlackjackState(BlackjackSnapshot? Snapshot, int? StateMessageId);
