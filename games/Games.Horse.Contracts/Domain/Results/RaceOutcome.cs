namespace Games.Horse.Domain.Results;

public sealed record RaceOutcome(
    HorseError Error,
    int Winner,
    byte[] GifBytes,
    List<RaceTransaction> Transactions,
    List<RacerSummary> Participants,
    List<long> BetScopeIds,
    string RaceDate = "");
