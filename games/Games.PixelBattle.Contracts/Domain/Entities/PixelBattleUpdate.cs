using System.Text.Json.Serialization;

namespace Games.PixelBattle.Domain.Entities;

public sealed record PixelBattleUpdate(int Index, string Color, string Versionstamp);
