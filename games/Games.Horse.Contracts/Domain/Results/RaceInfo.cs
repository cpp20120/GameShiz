namespace Games.Horse.Domain.Results;

public sealed record RaceInfo(int BetsCount, IReadOnlyDictionary<int, double> Koefs);
