namespace Games.PixelBattle.Contracts;

public sealed record PixelUpdateResult(PixelUpdateStatus Status, PixelBattleUpdate? Update = null);
