namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationEffectRow(
    Guid EffectId,
    string EffectType,
    string Importance,
    string Status,
    int Attempt,
    DateTime CreatedAt,
    DateTime NotBefore,
    string? LastErrorCode,
    string? LastErrorMessage);
