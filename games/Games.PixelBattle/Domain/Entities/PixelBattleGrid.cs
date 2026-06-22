using System.Text.Json.Serialization;

namespace Games.PixelBattle.Domain.Entities;

public sealed record PixelBattleGrid(string[] Tiles, string[] Versionstamps);
