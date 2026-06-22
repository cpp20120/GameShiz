namespace Games.Horse.Domain.Results;

public sealed record RaceInfo(int BetsCount, Dictionary<int, double> Koefs);
