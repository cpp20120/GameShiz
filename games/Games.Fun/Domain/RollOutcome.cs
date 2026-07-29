namespace Games.Fun.Domain;

public sealed record RollOutcome(
    string? Question,
    int Percentage,
    int? FavorableCases,
    int? TotalCases,
    RollBand Band);
