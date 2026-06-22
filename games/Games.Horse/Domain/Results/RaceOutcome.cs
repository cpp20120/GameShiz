namespace Games.Horse;

public sealed record RaceOutcome(
    HorseError Error,
    int Winner,
    byte[] GifBytes,
    List<RaceTransaction> Transactions,
    List<RacerSummary> Participants,
    List<long> BetScopeIds);
