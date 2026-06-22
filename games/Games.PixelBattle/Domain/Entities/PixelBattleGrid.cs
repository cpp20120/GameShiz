using System.Text.Json.Serialization;

namespace Games.PixelBattle;

public sealed record PixelBattleGrid(string[] Tiles, string[] Versionstamps);
