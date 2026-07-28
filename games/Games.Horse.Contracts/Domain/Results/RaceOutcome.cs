namespace Games.Horse.Domain.Results;

public sealed record RaceOutcome(
    HorseError Error,
    int Winner,
    byte[] GifBytes,
    IReadOnlyList<RaceTransaction> Transactions,
    IReadOnlyList<RacerSummary> Participants,
    IReadOnlyList<long> BetScopeIds,
    string RaceDate = "");
